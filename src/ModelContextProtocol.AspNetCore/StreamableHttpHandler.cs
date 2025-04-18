using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using ModelContextProtocol.Protocol.Messages;
using ModelContextProtocol.Protocol.Transport;
using ModelContextProtocol.Server;
using ModelContextProtocol.Utils.Json;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.IO.Pipelines;
using System.Security.Cryptography;
using System.Text.Json.Serialization.Metadata;

namespace ModelContextProtocol.AspNetCore;

internal sealed class StreamableHttpHandler(
    IOptions<McpServerOptions> mcpServerOptionsSnapshot,
    IOptionsFactory<McpServerOptions> mcpServerOptionsFactory,
    IOptions<HttpServerTransportOptions> httpMcpServerOptions,
    ILoggerFactory loggerFactory,
    IServiceProvider applicationServices)
{
    private static JsonTypeInfo<JsonRpcError> s_errorTypeInfo = GetRequiredJsonTypeInfo<JsonRpcError>();

    public ConcurrentDictionary<string, HttpMcpSession<StreamableHttpServerTransport>> Sessions { get; } = new(StringComparer.Ordinal);

    public async Task HandleRequestAsync(HttpContext context)
    {
        var session = await GetOrCreateSessionAsync(context);
        if (session is null)
        {
            return;
        }

        session.Reference();

        try
        {
            await HandleRequestAsync(context, session);
        }
        finally
        {
            session.Unreference();
        }
    }

    private async ValueTask HandleRequestAsync(HttpContext context, HttpMcpSession<StreamableHttpServerTransport> session)
    {
        var response = context.Response;
        response.Headers.ContentType = "text/event-stream";
        response.Headers.CacheControl = "no-cache,no-store";

        // Make sure we disable all response buffering for SSE.
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
                // We wound up writing nothing, so there should be no Content-Type response header.
                response.Headers.ContentType = (string?)null;
                response.StatusCode = StatusCodes.Status202Accepted;
            }
        }
        else if (string.Equals(HttpMethods.Delete, context.Request.Method, StringComparison.OrdinalIgnoreCase))
        {
            if (Sessions.TryRemove(session.Id, out var _))
            {
                await session.Transport.DisposeAsync();
            }
            return;
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
        HttpMcpSession<StreamableHttpServerTransport>? session;

        if (string.IsNullOrEmpty(sessionId))
        {
            session = await CreateSessionAsync(context);

            if (!Sessions.TryAdd(session.Id, session))
            {
                throw new UnreachableException($"Unreachable given good entropy! Session with ID '{sessionId}' has already been created.");
            }
        }
        else
        {
            session = await GetSessionAsync(context, sessionId);

            if (session is null)
            {
                return null;
            }
        }

        context.Response.Headers["mcp-session-id"] = session.Id;
        return session;
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
        // Use applicationServices instead of RequestServices since the session will likely outlive the first initialization request.
        var server = McpServerFactory.Create(transport, mcpServerOptions, loggerFactory, applicationServices);
        return new HttpMcpSession<StreamableHttpServerTransport>(MakeNewSessionId(), transport, context.User)
        {
            Server = server,
            ServerRunTask = (httpMcpServerOptions.Value.RunSessionHandler ?? RunSessionAsync)(context, server, context.RequestAborted),
        };
    }

    private async ValueTask<HttpMcpSession<StreamableHttpServerTransport>?> GetSessionAsync(HttpContext context, string sessionId)
    {
        if (Sessions.TryGetValue(sessionId, out var existingSession))
        {
            return existingSession;
        }

        // I'd consider making our ErrorCodes type public and reference that, but -32001 isn't part of the MCP standard.
        // This is what the typescript-sdk currently does. One of the few other usages I found was from some
        // Ethereum JSON-RPC documentation and this JSON-RPC library from Microsoft called StreamJsonRpc where it's called
        // JsonRpcErrorCode.NoMarshaledObjectFound
        // https://learn.microsoft.com/dotnet/api/streamjsonrpc.protocol.jsonrpcerrorcode?view=streamjsonrpc-2.9#fields
        await WriteJsonRpcErrorAsync(context, -32001, "Session not found", StatusCodes.Status404NotFound);
        return null;
    }

    private static Task WriteJsonRpcErrorAsync(HttpContext context, int errorCode, string errorMessage, int statusCode)
    {
        var jsonRpcError = new JsonRpcError
        {
            Error = new()
            {
                Code = errorCode,
                Message = errorMessage,
            },
        };
        return Results.Json(jsonRpcError, s_errorTypeInfo, statusCode: statusCode).ExecuteAsync(context);
    }

    internal static string MakeNewSessionId()
    {
        Span<byte> buffer = stackalloc byte[16];
        RandomNumberGenerator.Fill(buffer);
        return WebEncoders.Base64UrlEncode(buffer);
    }

    private static JsonTypeInfo<T> GetRequiredJsonTypeInfo<T>() => (JsonTypeInfo<T>)McpJsonUtilities.DefaultOptions.GetTypeInfo(typeof(T));

    private class HttpDuplexPipe(HttpContext context) : IDuplexPipe
    {
        public PipeReader Input => context.Request.BodyReader;
        public PipeWriter Output => context.Response.BodyWriter;
    }
}
