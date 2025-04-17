using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using ModelContextProtocol.Protocol.Transport;
using ModelContextProtocol.Server;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.IO.Pipelines;
using System.Security.Cryptography;

namespace ModelContextProtocol.AspNetCore;

internal sealed class StreamableHttpHandler(
    IOptions<McpServerOptions> mcpServerOptionsSnapshot,
    IOptionsFactory<McpServerOptions> mcpServerOptionsFactory,
    IOptions<HttpServerTransportOptions> httpMcpServerOptions,
    ILoggerFactory loggerFactory)
{
    public ConcurrentDictionary<string, HttpMcpSession<StreamableHttpServerTransport>> Sessions { get; } = new(StringComparer.Ordinal);

    public async Task HandleRequestAsync(HttpContext context)
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
            var wroteResponse = await session.Transport.HandlePostRequest(new HttpDuplexPipe(context), context.RequestAborted);
            if (!wroteResponse)
            {
                response.Headers.ContentType = (string?)null;
                response.StatusCode = StatusCodes.Status202Accepted;
            }
        }
        else
        {
            throw new UnreachableException($"Unexpected HTTP method: {context.Request.Method}.");
        }
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
            await Results.BadRequest($"Session ID not found.").ExecuteAsync(context);
            return null;
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

    private class HttpDuplexPipe(HttpContext context) : IDuplexPipe
    {
        public PipeReader Input => context.Request.BodyReader;
        public PipeWriter Output => context.Response.BodyWriter;
    }
}
