using ModelContextProtocol.Protocol;

namespace ModelContextProtocol.Server;

/// <summary>
/// Provides filter collections for MCP server handlers.
/// </summary>
/// <remarks>
/// This class contains collections of filters that can be applied to various MCP server handlers.
/// Filters are applied in reverse order, so the last filter added will be the outermost (first to execute).
/// This allows for middleware-style composition where filters can perform actions before and after the inner handler.
/// </remarks>
public sealed class McpServerFilters
{
    /// <summary>
    /// Gets the filters for the list tools handler pipeline.
    /// </summary>
    public List<Func<Func<RequestContext<ListToolsRequestParams>, CancellationToken, ValueTask<ListToolsResult>>, Func<RequestContext<ListToolsRequestParams>, CancellationToken, ValueTask<ListToolsResult>>>> ListToolsFilters { get; } = new();

    /// <summary>
    /// Gets the filters for the call tool handler pipeline.
    /// </summary>
    public List<Func<Func<RequestContext<CallToolRequestParams>, CancellationToken, ValueTask<CallToolResult>>, Func<RequestContext<CallToolRequestParams>, CancellationToken, ValueTask<CallToolResult>>>> CallToolFilters { get; } = new();

    /// <summary>
    /// Gets the filters for the list prompts handler pipeline.
    /// </summary>
    public List<Func<Func<RequestContext<ListPromptsRequestParams>, CancellationToken, ValueTask<ListPromptsResult>>, Func<RequestContext<ListPromptsRequestParams>, CancellationToken, ValueTask<ListPromptsResult>>>> ListPromptsFilters { get; } = new();

    /// <summary>
    /// Gets the filters for the get prompt handler pipeline.
    /// </summary>
    public List<Func<Func<RequestContext<GetPromptRequestParams>, CancellationToken, ValueTask<GetPromptResult>>, Func<RequestContext<GetPromptRequestParams>, CancellationToken, ValueTask<GetPromptResult>>>> GetPromptFilters { get; } = new();

    /// <summary>
    /// Gets the filters for the list resource templates handler pipeline.
    /// </summary>
    public List<Func<Func<RequestContext<ListResourceTemplatesRequestParams>, CancellationToken, ValueTask<ListResourceTemplatesResult>>, Func<RequestContext<ListResourceTemplatesRequestParams>, CancellationToken, ValueTask<ListResourceTemplatesResult>>>> ListResourceTemplatesFilters { get; } = new();

    /// <summary>
    /// Gets the filters for the list resources handler pipeline.
    /// </summary>
    public List<Func<Func<RequestContext<ListResourcesRequestParams>, CancellationToken, ValueTask<ListResourcesResult>>, Func<RequestContext<ListResourcesRequestParams>, CancellationToken, ValueTask<ListResourcesResult>>>> ListResourcesFilters { get; } = new();

    /// <summary>
    /// Gets the filters for the read resource handler pipeline.
    /// </summary>
    public List<Func<Func<RequestContext<ReadResourceRequestParams>, CancellationToken, ValueTask<ReadResourceResult>>, Func<RequestContext<ReadResourceRequestParams>, CancellationToken, ValueTask<ReadResourceResult>>>> ReadResourceFilters { get; } = new();

    /// <summary>
    /// Gets the filters for the complete handler pipeline.
    /// </summary>
    public List<Func<Func<RequestContext<CompleteRequestParams>, CancellationToken, ValueTask<CompleteResult>>, Func<RequestContext<CompleteRequestParams>, CancellationToken, ValueTask<CompleteResult>>>> CompleteFilters { get; } = new();

    /// <summary>
    /// Gets the filters for the subscribe to resources handler pipeline.
    /// </summary>
    public List<Func<Func<RequestContext<SubscribeRequestParams>, CancellationToken, ValueTask<EmptyResult>>, Func<RequestContext<SubscribeRequestParams>, CancellationToken, ValueTask<EmptyResult>>>> SubscribeToResourcesFilters { get; } = new();

    /// <summary>
    /// Gets the filters for the unsubscribe from resources handler pipeline.
    /// </summary>
    public List<Func<Func<RequestContext<UnsubscribeRequestParams>, CancellationToken, ValueTask<EmptyResult>>, Func<RequestContext<UnsubscribeRequestParams>, CancellationToken, ValueTask<EmptyResult>>>> UnsubscribeFromResourcesFilters { get; } = new();

    /// <summary>
    /// Gets the filters for the set logging level handler pipeline.
    /// </summary>
    public List<Func<Func<RequestContext<SetLevelRequestParams>, CancellationToken, ValueTask<EmptyResult>>, Func<RequestContext<SetLevelRequestParams>, CancellationToken, ValueTask<EmptyResult>>>> SetLoggingLevelFilters { get; } = new();
}
