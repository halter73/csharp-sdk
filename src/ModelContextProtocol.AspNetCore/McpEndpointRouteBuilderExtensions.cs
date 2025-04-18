using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Metadata;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using ModelContextProtocol.AspNetCore;
using System.Diagnostics.CodeAnalysis;

namespace Microsoft.AspNetCore.Builder;

/// <summary>
/// Provides extension methods for <see cref="IEndpointRouteBuilder"/> to add MCP endpoints.
/// </summary>
public static class McpEndpointRouteBuilderExtensions
{
    /// <summary>
    /// Sets up endpoints for handling MCP HTTP Streaming transport.
    /// See <see href="https://modelcontextprotocol.io/specification/2025-03-26/basic/transports#streamable-http">the 2025-03-26 protocol specification</see> for details about the Streamable HTTP transport.
    /// Also maps legacy SSE endpoints for backward compatibility at the path "/sse" and "/message". <see href="https://modelcontextprotocol.io/specification/2024-11-05/basic/transports#http-with-sse">the 2024-11-05 protocol specification</see> for details about the HTTP with SSE transport.
    /// </summary>
    /// <param name="endpoints">The web application to attach MCP HTTP endpoints.</param>
    /// <param name="pattern">The route pattern prefix to map to.</param>
    /// <returns>Returns a builder for configuring additional endpoint conventions like authorization policies.</returns>
    public static IEndpointConventionBuilder MapMcp(this IEndpointRouteBuilder endpoints, [StringSyntax("Route")] string pattern = "")
    {
        var streamableHttpHandler = endpoints.ServiceProvider.GetService<StreamableHttpHandler>() ??
            throw new InvalidOperationException("You must call WithHttpTransport(). Unable to find required services. Call builder.Services.AddMcpServer().WithHttpTransport() in application startup code.");

        var mcpGroup = endpoints.MapGroup(pattern);
        var streamableHttpGroup = mcpGroup.MapGroup("");
        streamableHttpGroup.MapPost("", streamableHttpHandler.HandleRequestAsync)
            .WithMetadata(new AcceptsMetadata(["application/json"]))
            .WithMetadata(new ProducesResponseTypeMetadata(StatusCodes.Status200OK, contentTypes: ["text/event-stream"]))
            .WithMetadata(new ProducesResponseTypeMetadata(StatusCodes.Status202Accepted));
        streamableHttpGroup.MapGet("", streamableHttpHandler.HandleRequestAsync)
            .WithMetadata(new ProducesResponseTypeMetadata(StatusCodes.Status200OK, contentTypes: ["text/event-stream"]));
        streamableHttpGroup.MapDelete("", streamableHttpHandler.HandleRequestAsync);
        streamableHttpGroup.WithDisplayName(b => $"MCP Streamable HTTP | {b.DisplayName}");

        // Map legacy HTTP with SSE endpoints.
        var sseHandler = endpoints.ServiceProvider.GetRequiredService<SseHandler>();
        var sseGroup = mcpGroup.MapGroup("");
        sseGroup.MapGet("/sse", sseHandler.HandleSseRequestAsync)
            .WithMetadata(new ProducesResponseTypeMetadata(StatusCodes.Status200OK, contentTypes: ["text/event-stream"]));
        sseGroup.MapPost("/message", sseHandler.HandleMessageRequestAsync)
            .WithMetadata(new AcceptsMetadata(["application/json"]))
            .WithMetadata(new ProducesResponseTypeMetadata(StatusCodes.Status202Accepted));
        sseGroup.WithDisplayName(b => $"MCP HTTP with SSE | {b.DisplayName}");

        return mcpGroup;
    }
}
