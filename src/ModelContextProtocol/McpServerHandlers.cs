using Microsoft.Extensions.DependencyInjection;
using ModelContextProtocol.Protocol;

namespace ModelContextProtocol.Server;

/// <summary>
/// Provides a container for handlers used in the creation of an MCP server.
/// </summary>
/// <remarks>
/// <para>
/// This class provides a centralized collection of delegates that implement various capabilities of the Model Context Protocol.
/// Each handler in this class corresponds to a specific endpoint in the Model Context Protocol and
/// is responsible for processing a particular type of request. The handlers are used to customize
/// the behavior of the MCP server by providing implementations for the various protocol operations.
/// </para>
/// <para>
/// Handlers can be configured individually using the extension methods in <see cref="McpServerBuilderExtensions"/>
/// such as <see cref="McpServerBuilderExtensions.WithListToolsHandler"/> and
/// <see cref="McpServerBuilderExtensions.WithCallToolHandler"/>.
/// </para>
/// <para>
/// When a client sends a request to the server, the appropriate handler is invoked to process the
/// request and produce a response according to the protocol specification. Which handler is selected
/// is done based on an ordinal, case-sensitive string comparison.
/// </para>
/// </remarks>
public sealed class McpServerHandlers
{
    /// <summary>
    /// Gets or sets the handler for <see cref="RequestMethods.ToolsList"/> requests.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The handler should return a list of available tools when requested by a client.
    /// It supports pagination through the cursor mechanism, where the client can make
    /// repeated calls with the cursor returned by the previous call to retrieve more tools.
    /// </para>
    /// <para>
    /// This handler works alongside any tools defined in the <see cref="McpServerTool"/> collection.
    /// Tools from both sources will be combined when returning results to clients.
    /// </para>
    /// </remarks>
    public Func<RequestContext<ListToolsRequestParams>, CancellationToken, ValueTask<ListToolsResult>>? ListToolsHandler { get; set; }

    /// <summary>
    /// Gets or sets the handler for <see cref="RequestMethods.ToolsCall"/> requests.
    /// </summary>
    /// <remarks>
    /// This handler is invoked when a client makes a call to a tool that isn't found in the <see cref="McpServerTool"/> collection.
    /// The handler should implement logic to execute the requested tool and return appropriate results.
    /// </remarks>
    public Func<RequestContext<CallToolRequestParams>, CancellationToken, ValueTask<CallToolResult>>? CallToolHandler { get; set; }

    /// <summary>
    /// Gets or sets the handler for <see cref="RequestMethods.PromptsList"/> requests.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The handler should return a list of available prompts when requested by a client.
    /// It supports pagination through the cursor mechanism, where the client can make
    /// repeated calls with the cursor returned by the previous call to retrieve more prompts.
    /// </para>
    /// <para>
    /// This handler works alongside any prompts defined in the <see cref="McpServerPrompt"/> collection.
    /// Prompts from both sources will be combined when returning results to clients.
    /// </para>
    /// </remarks>
    public Func<RequestContext<ListPromptsRequestParams>, CancellationToken, ValueTask<ListPromptsResult>>? ListPromptsHandler { get; set; }

    /// <summary>
    /// Gets or sets the handler for <see cref="RequestMethods.PromptsGet"/> requests.
    /// </summary>
    /// <remarks>
    /// This handler is invoked when a client requests details for a specific prompt that isn't found in the <see cref="McpServerPrompt"/> collection.
    /// The handler should implement logic to fetch or generate the requested prompt and return appropriate results.
    /// </remarks>
    public Func<RequestContext<GetPromptRequestParams>, CancellationToken, ValueTask<GetPromptResult>>? GetPromptHandler { get; set; }

    /// <summary>
    /// Gets or sets the handler for <see cref="RequestMethods.ResourcesTemplatesList"/> requests.
    /// </summary>
    /// <remarks>
    /// The handler should return a list of available resource templates when requested by a client.
    /// It supports pagination through the cursor mechanism, where the client can make
    /// repeated calls with the cursor returned by the previous call to retrieve more resource templates.
    /// </remarks>
    public Func<RequestContext<ListResourceTemplatesRequestParams>, CancellationToken, ValueTask<ListResourceTemplatesResult>>? ListResourceTemplatesHandler { get; set; }

    /// <summary>
    /// Gets or sets the handler for <see cref="RequestMethods.ResourcesList"/> requests.
    /// </summary>
    /// <remarks>
    /// The handler should return a list of available resources when requested by a client.
    /// It supports pagination through the cursor mechanism, where the client can make
    /// repeated calls with the cursor returned by the previous call to retrieve more resources.
    /// </remarks>
    public Func<RequestContext<ListResourcesRequestParams>, CancellationToken, ValueTask<ListResourcesResult>>? ListResourcesHandler { get; set; }

    /// <summary>
    /// Gets or sets the handler for <see cref="RequestMethods.ResourcesRead"/> requests.
    /// </summary>
    /// <remarks>
    /// This handler is invoked when a client requests the content of a specific resource identified by its URI.
    /// The handler should implement logic to locate and retrieve the requested resource.
    /// </remarks>
    public Func<RequestContext<ReadResourceRequestParams>, CancellationToken, ValueTask<ReadResourceResult>>? ReadResourceHandler { get; set; }

    /// <summary>
    /// Gets or sets the handler for <see cref="RequestMethods.CompletionComplete"/> requests.
    /// </summary>
    /// <remarks>
    /// This handler provides auto-completion suggestions for prompt arguments or resource references in the Model Context Protocol.
    /// The handler processes auto-completion requests, returning a list of suggestions based on the 
    /// reference type and current argument value.
    /// </remarks>
    public Func<RequestContext<CompleteRequestParams>, CancellationToken, ValueTask<CompleteResult>>? CompleteHandler { get; set; }

    /// <summary>
    /// Gets or sets the handler for <see cref="RequestMethods.ResourcesSubscribe"/> requests.
    /// </summary>
    /// <remarks>
    /// <para>
    /// This handler is invoked when a client wants to receive notifications about changes to specific resources or resource patterns.
    /// The handler should implement logic to register the client's interest in the specified resources
    /// and set up the necessary infrastructure to send notifications when those resources change.
    /// </para>
    /// <para>
    /// After a successful subscription, the server should send resource change notifications to the client
    /// whenever a relevant resource is created, updated, or deleted.
    /// </para>
    /// </remarks>
    public Func<RequestContext<SubscribeRequestParams>, CancellationToken, ValueTask<EmptyResult>>? SubscribeToResourcesHandler { get; set; }

    /// <summary>
    /// Gets or sets the handler for <see cref="RequestMethods.ResourcesUnsubscribe"/> requests.
    /// </summary>
    /// <remarks>
    /// <para>
    /// This handler is invoked when a client wants to stop receiving notifications about previously subscribed resources.
    /// The handler should implement logic to remove the client's subscriptions to the specified resources
    /// and clean up any associated resources.
    /// </para>
    /// <para>
    /// After a successful unsubscription, the server should no longer send resource change notifications
    /// to the client for the specified resources.
    /// </para>
    /// </remarks>
    public Func<RequestContext<UnsubscribeRequestParams>, CancellationToken, ValueTask<EmptyResult>>? UnsubscribeFromResourcesHandler { get; set; }

    /// <summary>
    /// Gets or sets the handler for <see cref="RequestMethods.LoggingSetLevel"/> requests.
    /// </summary>
    /// <remarks>
    /// <para>
    /// This handler processes <see cref="RequestMethods.LoggingSetLevel"/> requests from clients. When set, it enables
    /// clients to control which log messages they receive by specifying a minimum severity threshold.
    /// </para>
    /// <para>
    /// After handling a level change request, the server typically begins sending log messages
    /// at or above the specified level to the client as notifications/message notifications.
    /// </para>
    /// </remarks>
    public Func<RequestContext<SetLevelRequestParams>, CancellationToken, ValueTask<EmptyResult>>? SetLoggingLevelHandler { get; set; }

    #region Filter Collections
    private readonly List<Func<Func<RequestContext<ListResourceTemplatesRequestParams>, CancellationToken, ValueTask<ListResourceTemplatesResult>>, Func<RequestContext<ListResourceTemplatesRequestParams>, CancellationToken, ValueTask<ListResourceTemplatesResult>>>> _listResourceTemplatesFilters = new();
    private readonly List<Func<Func<RequestContext<ListToolsRequestParams>, CancellationToken, ValueTask<ListToolsResult>>, Func<RequestContext<ListToolsRequestParams>, CancellationToken, ValueTask<ListToolsResult>>>> _listToolsFilters = new();
    private readonly List<Func<Func<RequestContext<CallToolRequestParams>, CancellationToken, ValueTask<CallToolResult>>, Func<RequestContext<CallToolRequestParams>, CancellationToken, ValueTask<CallToolResult>>>> _callToolFilters = new();
    private readonly List<Func<Func<RequestContext<ListPromptsRequestParams>, CancellationToken, ValueTask<ListPromptsResult>>, Func<RequestContext<ListPromptsRequestParams>, CancellationToken, ValueTask<ListPromptsResult>>>> _listPromptsFilters = new();
    private readonly List<Func<Func<RequestContext<GetPromptRequestParams>, CancellationToken, ValueTask<GetPromptResult>>, Func<RequestContext<GetPromptRequestParams>, CancellationToken, ValueTask<GetPromptResult>>>> _getPromptFilters = new();
    private readonly List<Func<Func<RequestContext<ListResourcesRequestParams>, CancellationToken, ValueTask<ListResourcesResult>>, Func<RequestContext<ListResourcesRequestParams>, CancellationToken, ValueTask<ListResourcesResult>>>> _listResourcesFilters = new();
    private readonly List<Func<Func<RequestContext<ReadResourceRequestParams>, CancellationToken, ValueTask<ReadResourceResult>>, Func<RequestContext<ReadResourceRequestParams>, CancellationToken, ValueTask<ReadResourceResult>>>> _readResourceFilters = new();
    private readonly List<Func<Func<RequestContext<CompleteRequestParams>, CancellationToken, ValueTask<CompleteResult>>, Func<RequestContext<CompleteRequestParams>, CancellationToken, ValueTask<CompleteResult>>>> _completeFilters = new();
    private readonly List<Func<Func<RequestContext<SubscribeRequestParams>, CancellationToken, ValueTask<EmptyResult>>, Func<RequestContext<SubscribeRequestParams>, CancellationToken, ValueTask<EmptyResult>>>> _subscribeToResourcesFilters = new();
    private readonly List<Func<Func<RequestContext<UnsubscribeRequestParams>, CancellationToken, ValueTask<EmptyResult>>, Func<RequestContext<UnsubscribeRequestParams>, CancellationToken, ValueTask<EmptyResult>>>> _unsubscribeFromResourcesFilters = new();
    private readonly List<Func<Func<RequestContext<SetLevelRequestParams>, CancellationToken, ValueTask<EmptyResult>>, Func<RequestContext<SetLevelRequestParams>, CancellationToken, ValueTask<EmptyResult>>>> _setLoggingLevelFilters = new();
    #endregion

    #region Filter Registration Methods
    /// <summary>
    /// Adds a filter to the list resource templates handler pipeline.
    /// </summary>
    /// <param name="filter">The filter to add.</param>
    internal void AddListResourceTemplatesFilter(Func<Func<RequestContext<ListResourceTemplatesRequestParams>, CancellationToken, ValueTask<ListResourceTemplatesResult>>, Func<RequestContext<ListResourceTemplatesRequestParams>, CancellationToken, ValueTask<ListResourceTemplatesResult>>> filter)
    {
        _listResourceTemplatesFilters.Add(filter);
    }

    /// <summary>
    /// Adds a filter to the list tools handler pipeline.
    /// </summary>
    /// <param name="filter">The filter to add.</param>
    internal void AddListToolsFilter(Func<Func<RequestContext<ListToolsRequestParams>, CancellationToken, ValueTask<ListToolsResult>>, Func<RequestContext<ListToolsRequestParams>, CancellationToken, ValueTask<ListToolsResult>>> filter)
    {
        _listToolsFilters.Add(filter);
    }

    /// <summary>
    /// Adds a filter to the call tool handler pipeline.
    /// </summary>
    /// <param name="filter">The filter to add.</param>
    internal void AddCallToolFilter(Func<Func<RequestContext<CallToolRequestParams>, CancellationToken, ValueTask<CallToolResult>>, Func<RequestContext<CallToolRequestParams>, CancellationToken, ValueTask<CallToolResult>>> filter)
    {
        _callToolFilters.Add(filter);
    }

    /// <summary>
    /// Adds a filter to the list prompts handler pipeline.
    /// </summary>
    /// <param name="filter">The filter to add.</param>
    internal void AddListPromptsFilter(Func<Func<RequestContext<ListPromptsRequestParams>, CancellationToken, ValueTask<ListPromptsResult>>, Func<RequestContext<ListPromptsRequestParams>, CancellationToken, ValueTask<ListPromptsResult>>> filter)
    {
        _listPromptsFilters.Add(filter);
    }

    /// <summary>
    /// Adds a filter to the get prompt handler pipeline.
    /// </summary>
    /// <param name="filter">The filter to add.</param>
    internal void AddGetPromptFilter(Func<Func<RequestContext<GetPromptRequestParams>, CancellationToken, ValueTask<GetPromptResult>>, Func<RequestContext<GetPromptRequestParams>, CancellationToken, ValueTask<GetPromptResult>>> filter)
    {
        _getPromptFilters.Add(filter);
    }

    /// <summary>
    /// Adds a filter to the list resources handler pipeline.
    /// </summary>
    /// <param name="filter">The filter to add.</param>
    internal void AddListResourcesFilter(Func<Func<RequestContext<ListResourcesRequestParams>, CancellationToken, ValueTask<ListResourcesResult>>, Func<RequestContext<ListResourcesRequestParams>, CancellationToken, ValueTask<ListResourcesResult>>> filter)
    {
        _listResourcesFilters.Add(filter);
    }

    /// <summary>
    /// Adds a filter to the read resource handler pipeline.
    /// </summary>
    /// <param name="filter">The filter to add.</param>
    internal void AddReadResourceFilter(Func<Func<RequestContext<ReadResourceRequestParams>, CancellationToken, ValueTask<ReadResourceResult>>, Func<RequestContext<ReadResourceRequestParams>, CancellationToken, ValueTask<ReadResourceResult>>> filter)
    {
        _readResourceFilters.Add(filter);
    }

    /// <summary>
    /// Adds a filter to the complete handler pipeline.
    /// </summary>
    /// <param name="filter">The filter to add.</param>
    internal void AddCompleteFilter(Func<Func<RequestContext<CompleteRequestParams>, CancellationToken, ValueTask<CompleteResult>>, Func<RequestContext<CompleteRequestParams>, CancellationToken, ValueTask<CompleteResult>>> filter)
    {
        _completeFilters.Add(filter);
    }

    /// <summary>
    /// Adds a filter to the subscribe to resources handler pipeline.
    /// </summary>
    /// <param name="filter">The filter to add.</param>
    internal void AddSubscribeToResourcesFilter(Func<Func<RequestContext<SubscribeRequestParams>, CancellationToken, ValueTask<EmptyResult>>, Func<RequestContext<SubscribeRequestParams>, CancellationToken, ValueTask<EmptyResult>>> filter)
    {
        _subscribeToResourcesFilters.Add(filter);
    }

    /// <summary>
    /// Adds a filter to the unsubscribe from resources handler pipeline.
    /// </summary>
    /// <param name="filter">The filter to add.</param>
    internal void AddUnsubscribeFromResourcesFilter(Func<Func<RequestContext<UnsubscribeRequestParams>, CancellationToken, ValueTask<EmptyResult>>, Func<RequestContext<UnsubscribeRequestParams>, CancellationToken, ValueTask<EmptyResult>>> filter)
    {
        _unsubscribeFromResourcesFilters.Add(filter);
    }

    /// <summary>
    /// Adds a filter to the set logging level handler pipeline.
    /// </summary>
    /// <param name="filter">The filter to add.</param>
    internal void AddSetLoggingLevelFilter(Func<Func<RequestContext<SetLevelRequestParams>, CancellationToken, ValueTask<EmptyResult>>, Func<RequestContext<SetLevelRequestParams>, CancellationToken, ValueTask<EmptyResult>>> filter)
    {
        _setLoggingLevelFilters.Add(filter);
    }
    #endregion

    #region Helper Methods for Building Filter Pipelines
    /// <summary>
    /// Builds a filter pipeline from a list of filters, applying them in reverse order so the last filter is outermost.
    /// </summary>
    /// <typeparam name="THandler">The handler delegate type.</typeparam>
    /// <param name="baseHandler">The base handler to wrap with filters.</param>
    /// <param name="filters">The list of filters to apply.</param>
    /// <returns>The wrapped handler with all filters applied.</returns>
    private static THandler BuildFilterPipeline<THandler>(THandler baseHandler, List<Func<THandler, THandler>> filters)
        where THandler : Delegate
    {
        // Apply filters in reverse order so the last added filter is the outermost (first to execute)
        THandler current = baseHandler;
        for (int i = filters.Count - 1; i >= 0; i--)
        {
            current = filters[i](current);
        }
        return current;
    }

    /// <summary>
    /// Wraps the existing handler (if any) with the filter pipeline and returns the result.
    /// </summary>
    /// <typeparam name="TRequest">The request type.</typeparam>
    /// <typeparam name="TResult">The result type.</typeparam>
    /// <param name="existingHandler">The existing handler from options (may be null).</param>
    /// <param name="newHandler">The new handler from this instance (may be null).</param>
    /// <param name="filters">The list of filters to apply.</param>
    /// <returns>The final handler with filters applied, or null if no handlers are available.</returns>
    private static Func<TRequest, CancellationToken, ValueTask<TResult>>? WrapHandlerWithFilters<TRequest, TResult>(
        Func<TRequest, CancellationToken, ValueTask<TResult>>? existingHandler,
        Func<TRequest, CancellationToken, ValueTask<TResult>>? newHandler,
        List<Func<Func<TRequest, CancellationToken, ValueTask<TResult>>, Func<TRequest, CancellationToken, ValueTask<TResult>>>> filters)
    {
        var baseHandler = newHandler ?? existingHandler;
        if (baseHandler is null || filters.Count == 0)
        {
            return baseHandler;
        }

        // Convert to delegate type and build pipeline
        var typedHandler = baseHandler;
        var wrappedHandler = BuildFilterPipeline(typedHandler, filters);
        return wrappedHandler;
    }
    #endregion

    /// <summary>
    /// Overwrite any handlers in McpServerOptions with non-null handlers from this instance.
    /// </summary>
    /// <param name="options"></param>
    /// <returns></returns>
    internal void OverwriteWithSetHandlers(McpServerOptions options)
    {
        PromptsCapability? promptsCapability = options.Capabilities?.Prompts;
        if (ListPromptsHandler is not null || GetPromptHandler is not null || _listPromptsFilters.Count > 0 || _getPromptFilters.Count > 0)
        {
            promptsCapability ??= new();
            promptsCapability.ListPromptsHandler = WrapHandlerWithFilters<RequestContext<ListPromptsRequestParams>, ListPromptsResult>(
                promptsCapability.ListPromptsHandler, ListPromptsHandler, _listPromptsFilters);
            promptsCapability.GetPromptHandler = WrapHandlerWithFilters<RequestContext<GetPromptRequestParams>, GetPromptResult>(
                promptsCapability.GetPromptHandler, GetPromptHandler, _getPromptFilters);
        }

        ResourcesCapability? resourcesCapability = options.Capabilities?.Resources;
        if (ListResourcesHandler is not null ||
            ReadResourceHandler is not null ||
            ListResourceTemplatesHandler is not null ||
            _listResourcesFilters.Count > 0 ||
            _readResourceFilters.Count > 0 ||
            _listResourceTemplatesFilters.Count > 0)
        {
            resourcesCapability ??= new();
            resourcesCapability.ListResourceTemplatesHandler = WrapHandlerWithFilters<RequestContext<ListResourceTemplatesRequestParams>, ListResourceTemplatesResult>(
                resourcesCapability.ListResourceTemplatesHandler, ListResourceTemplatesHandler, _listResourceTemplatesFilters);
            resourcesCapability.ListResourcesHandler = WrapHandlerWithFilters<RequestContext<ListResourcesRequestParams>, ListResourcesResult>(
                resourcesCapability.ListResourcesHandler, ListResourcesHandler, _listResourcesFilters);
            resourcesCapability.ReadResourceHandler = WrapHandlerWithFilters<RequestContext<ReadResourceRequestParams>, ReadResourceResult>(
                resourcesCapability.ReadResourceHandler, ReadResourceHandler, _readResourceFilters);

            if (SubscribeToResourcesHandler is not null || UnsubscribeFromResourcesHandler is not null ||
                _subscribeToResourcesFilters.Count > 0 || _unsubscribeFromResourcesFilters.Count > 0)
            {
                resourcesCapability.SubscribeToResourcesHandler = WrapHandlerWithFilters<RequestContext<SubscribeRequestParams>, EmptyResult>(
                    resourcesCapability.SubscribeToResourcesHandler, SubscribeToResourcesHandler, _subscribeToResourcesFilters);
                resourcesCapability.UnsubscribeFromResourcesHandler = WrapHandlerWithFilters<RequestContext<UnsubscribeRequestParams>, EmptyResult>(
                    resourcesCapability.UnsubscribeFromResourcesHandler, UnsubscribeFromResourcesHandler, _unsubscribeFromResourcesFilters);
                resourcesCapability.Subscribe = true;
            }
        }

        ToolsCapability? toolsCapability = options.Capabilities?.Tools;
        if (ListToolsHandler is not null || CallToolHandler is not null || _listToolsFilters.Count > 0 || _callToolFilters.Count > 0)
        {
            toolsCapability ??= new();
            toolsCapability.ListToolsHandler = WrapHandlerWithFilters<RequestContext<ListToolsRequestParams>, ListToolsResult>(
                toolsCapability.ListToolsHandler, ListToolsHandler, _listToolsFilters);
            toolsCapability.CallToolHandler = WrapHandlerWithFilters<RequestContext<CallToolRequestParams>, CallToolResult>(
                toolsCapability.CallToolHandler, CallToolHandler, _callToolFilters);
        }

        LoggingCapability? loggingCapability = options.Capabilities?.Logging;
        if (SetLoggingLevelHandler is not null || _setLoggingLevelFilters.Count > 0)
        {
            loggingCapability ??= new();
            loggingCapability.SetLoggingLevelHandler = WrapHandlerWithFilters<RequestContext<SetLevelRequestParams>, EmptyResult>(
                loggingCapability.SetLoggingLevelHandler, SetLoggingLevelHandler, _setLoggingLevelFilters);
        }

        CompletionsCapability? completionsCapability = options.Capabilities?.Completions;
        if (CompleteHandler is not null || _completeFilters.Count > 0)
        {
            completionsCapability ??= new();
            completionsCapability.CompleteHandler = WrapHandlerWithFilters<RequestContext<CompleteRequestParams>, CompleteResult>(
                completionsCapability.CompleteHandler, CompleteHandler, _completeFilters);
        }

        options.Capabilities ??= new();
        options.Capabilities.Prompts = promptsCapability;
        options.Capabilities.Resources = resourcesCapability;
        options.Capabilities.Tools = toolsCapability;
        options.Capabilities.Logging = loggingCapability;
        options.Capabilities.Completions = completionsCapability;
    }
}
