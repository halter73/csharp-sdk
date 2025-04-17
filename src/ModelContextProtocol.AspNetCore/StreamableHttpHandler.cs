using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using ModelContextProtocol.Protocol.Transport;
using ModelContextProtocol.Server;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Security.Cryptography;

namespace ModelContextProtocol.AspNetCore;

internal sealed class StreamableHttpHandler(
    IOptions<McpServerOptions> mcpServerOptionsSnapshot,
    IOptionsFactory<McpServerOptions> mcpServerOptionsFactory,
    IOptions<HttpServerTransportOptions> httpMcpServerOptions,
    IHostApplicationLifetime hostApplicationLifetime,
    ILoggerFactory loggerFactory)
{
    public ConcurrentDictionary<string, HttpMcpSession<StreamableHttpServerTransport>> Sessions { get; } = new(StringComparer.Ordinal);

    public async ValueTask HandleRequestAsync(HttpContext context)
    {
        var session = await GetOrCreateSessionAsync(context).ConfigureAwait(false);
        if (session is null)
        {
            return;
        }

        var response = context.Response;
        response.Headers.ContentType = "text/event-stream";
        response.Headers.CacheControl = "no-cache,no-store";

        // Make sure we disable all response buffering for SSE
        context.Response.Headers.ContentEncoding = "identity";
        context.Features.GetRequiredFeature<IHttpResponseBodyFeature>().DisableBuffering();

        if (string.Equals(HttpMethods.Get, context.Request.Method, StringComparison.OrdinalIgnoreCase))
        {
            await session.Transport.HandleGetRequest(context.Response.Body, context.RequestAborted);
        }
        else if (string.Equals(HttpMethods.Post, context.Request.Method, StringComparison.OrdinalIgnoreCase))
        {
            await session.Transport.HandlePostRequest(context.Response.Body, context.RequestAborted);
        }
        else
        {
            throw new UnreachableException($"Unexpected HTTP method: {context.Request.Method}.");
        }
    }

    public async ValueTask HandleSseResponseAsync(HttpContext context)
    {
        var session = GetOrCreateSessionAsync(context);

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

        var transport = new StreamableHttpServerTransport(response.BodyWriter);
        var httpMcpSession = new HttpMcpSession<StreamableHttpServerTransport>(transport, context.User);
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

                var runSessionAsync = httpMcpServerOptions.Value.RunSessionHandler ?? RunSessionAsync;
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

    public async ValueTask HandlePostRequestAsync(HttpContext context)
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
        // Full duplex messages are not supported by the Streamable HTTP spec, but it would be easy for us to support
        // by running OnPostBodyReceivedAsync in parallel to the response writing loop in HandleSseRequestAsync.
        await httpMcpSession.Transport.OnPostBodyReceivedAsync(context.Request.BodyReader, context.RequestAborted);
        await HandleSseResponseAsync(context);
    }

    internal static Task RunSessionAsync(HttpContext httpContext, IMcpServer session, CancellationToken requestAborted)
        => session.RunAsync(requestAborted);

    private async ValueTask<HttpMcpSession<StreamableHttpServerTransport>?> GetOrCreateSessionAsync(HttpContext context)
    {
        var sessionId = context.Request.Headers["mcp-session-id"].ToString();

        if (string.IsNullOrEmpty(sessionId))
        {
            sessionId = MakeNewSessionId();
            var newSession = await CreateSessionAsync(context).ConfigureAwait(false);

            if (!Sessions.TryAdd(sessionId, newSession))
            {
                throw new UnreachableException($"Unreachable given good entropy! Session with ID '{sessionId}' has already been created.");
            }

            return newSession;
        }

        if (!Sessions.TryGetValue(sessionId, out var existingSession))
        {
            throw new McpException("Session ID not found.");
        }

        if (!existingSession.HasSameUserId(context.User))
        {
            await Results.Forbid().ExecuteAsync(context);
            return null;
        }

        return existingSession;
    }

    private async ValueTask<HttpMcpSession<StreamableHttpServerTransport>> CreateSessionAsync(HttpContext context)
    {
        var mcpServerOptions = mcpServerOptionsSnapshot.Value;
        if (httpMcpServerOptions.Value.ConfigureSessionOptions is { } configureSessionOptions)
        {
            mcpServerOptions = mcpServerOptionsFactory.Create(Options.DefaultName);
            await configureSessionOptions(context, mcpServerOptions, context.RequestAborted);
        }

        var transport = new StreamableHttpServerTransport();
        var server = McpServerFactory.Create(transport, mcpServerOptions, loggerFactory, context.RequestServices);
        return new HttpMcpSession<StreamableHttpServerTransport>(transport, context.User)
        {
            Server = server,
            ServerRunTask = (httpMcpServerOptions.Value.RunSessionHandler ?? RunSessionAsync)(context, server, context.RequestAborted),
        };
    }

    internal static string MakeNewSessionId()
    {
        Span<byte> buffer = stackalloc byte[16];
        RandomNumberGenerator.Fill(buffer);
        return WebEncoders.Base64UrlEncode(buffer);
    }
}
