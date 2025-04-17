using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using ModelContextProtocol.Protocol.Messages;
using ModelContextProtocol.Protocol.Transport;
using ModelContextProtocol.Server;
using ModelContextProtocol.Utils.Json;
using System.Collections.Concurrent;
using System.Diagnostics;

namespace ModelContextProtocol.AspNetCore;

internal sealed class SseHandler(
    IOptions<McpServerOptions> mcpServerOptionsSnapshot,
    IOptionsFactory<McpServerOptions> mcpServerOptionsFactory,
    IOptions<HttpServerTransportOptions> httpMcpServerOptions,
    IHostApplicationLifetime hostApplicationLifetime,
    ILoggerFactory loggerFactory)
{
    private readonly ConcurrentDictionary<string, HttpMcpSession<SseResponseStreamTransport>> _sessions = new(StringComparer.Ordinal);
    private readonly ILogger _logger = loggerFactory.CreateLogger<SseHandler>();

    public async Task HandleRequestAsync(HttpContext context)
    {
        if (string.Equals(HttpMethods.Get, context.Request.Method, StringComparison.OrdinalIgnoreCase))
        {
            await HandleSseRequestAsync(context);
        }
        else if (string.Equals(HttpMethods.Post, context.Request.Method, StringComparison.OrdinalIgnoreCase))
        {
            await HandleMessageRequestAsync(context);
        }
        else
        {
            throw new UnreachableException($"Unexpected HTTP method: {context.Request.Method}.");
        }
    }

    public async Task HandleSseRequestAsync(HttpContext context)
    {
        var sessionId = StreamableHttpHandler.MakeNewSessionId();

        // If the server is shutting down, we need to cancel all SSE connections immediately without waiting for HostOptions.ShutdownTimeout
        // which defaults to 30 seconds.
        using var sseCts = CancellationTokenSource.CreateLinkedTokenSource(context.RequestAborted, hostApplicationLifetime.ApplicationStopping);
        var cancellationToken = sseCts.Token;

        var response = context.Response;
        response.Headers.ContentType = "text/event-stream";
        response.Headers.CacheControl = "no-cache,no-store";

        // Make sure we disable all response buffering for SSE
        context.Response.Headers.ContentEncoding = "identity";
        context.Features.GetRequiredFeature<IHttpResponseBodyFeature>().DisableBuffering();

        await using var transport = new SseResponseStreamTransport(response.Body, $"message?sessionId={sessionId}");
        var httpMcpSession = new HttpMcpSession<SseResponseStreamTransport>(transport, context.User);
        if (!_sessions.TryAdd(sessionId, httpMcpSession))
        {
            Debug.Fail("Unreachable given good entropy!");
            throw new InvalidOperationException($"Session with ID '{sessionId}' has already been created.");
        }

        try
        {
            var mcpServerOptions = mcpServerOptionsSnapshot.Value;
            if (httpMcpServerOptions.Value.ConfigureSessionOptions is { } configureSessionOptions)
            {
                mcpServerOptions = mcpServerOptionsFactory.Create(Options.DefaultName);
                await configureSessionOptions(context, mcpServerOptions, cancellationToken);
            }

            var transportTask = transport.RunAsync(cancellationToken);

            try
            {
                await using var mcpServer = McpServerFactory.Create(transport, mcpServerOptions, loggerFactory, context.RequestServices);
                context.Features.Set(mcpServer);

                var runSessionAsync = httpMcpServerOptions.Value.RunSessionHandler ?? StreamableHttpHandler.RunSessionAsync;
                await runSessionAsync(context, mcpServer, cancellationToken);
            }
            finally
            {
                await transport.DisposeAsync();
                await transportTask;
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            // RequestAborted always triggers when the client disconnects before a complete response body is written,
            // but this is how SSE connections are typically closed.
        }
        finally
        {
            _sessions.TryRemove(sessionId, out _);
        }
    }

    public async Task HandleMessageRequestAsync(HttpContext context)
    {
        if (!context.Request.Query.TryGetValue("sessionId", out var sessionId))
        {
            await Results.BadRequest("Missing sessionId query parameter.").ExecuteAsync(context);
            return;
        }

        if (!_sessions.TryGetValue(sessionId.ToString(), out var httpMcpSession))
        {
            await Results.BadRequest($"Session ID not found.").ExecuteAsync(context);
            return;
        }

        if (!httpMcpSession.HasSameUserId(context.User))
        {
            await Results.Forbid().ExecuteAsync(context);
            return;
        }

        var message = (JsonRpcMessage?)await context.Request.ReadFromJsonAsync(McpJsonUtilities.DefaultOptions.GetTypeInfo(typeof(JsonRpcMessage)), context.RequestAborted);
        if (message is null)
        {
            await Results.BadRequest("No message in request body.").ExecuteAsync(context);
            return;
        }

        await httpMcpSession.Transport.OnMessageReceivedAsync(message, context.RequestAborted);
        context.Response.StatusCode = StatusCodes.Status202Accepted;
        await context.Response.WriteAsync("Accepted");
    }
}
