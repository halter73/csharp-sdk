# MCP Server Handler Filters

This document describes the filter functionality in the MCP Server, which allows you to add middleware-style filters to handler pipelines.

## Overview

For each handler type in the MCP Server, there are corresponding `AddXXXFilter` methods in `McpServerBuilderExtensions.cs` that allow you to add filters to the handler pipeline. The filters are stored in `McpServerOptions.Filters` and applied during server configuration.

## Filter Architecture

### Filter Storage
Filters are now stored in the `McpServerOptions.Filters` property, which is of type `McpServerFilters`. This class contains public collections for each handler type:

```csharp
public sealed class McpServerFilters
{
    public List<Func<Func<RequestContext<ListToolsRequestParams>, CancellationToken, ValueTask<ListToolsResult>>, Func<RequestContext<ListToolsRequestParams>, CancellationToken, ValueTask<ListToolsResult>>>> ListToolsFilters { get; }
    public List<Func<Func<RequestContext<CallToolRequestParams>, CancellationToken, ValueTask<CallToolResult>>, Func<RequestContext<CallToolRequestParams>, CancellationToken, ValueTask<CallToolResult>>>> CallToolFilters { get; }
    // ... and so on for each handler type
}
```

### Filter Application
Filters are applied in the `McpServer` configuration methods (`ConfigureTools`, `ConfigureResources`, `ConfigurePrompts`, etc.) rather than in `McpServerHandlers`. This provides better separation of concerns and makes the filter pipeline more explicit.

## Available Filter Methods

The following filter methods are available:

- `AddListResourceTemplatesFilter` - Filter for list resource templates handlers
- `AddListToolsFilter` - Filter for list tools handlers
- `AddCallToolFilter` - Filter for call tool handlers
- `AddListPromptsFilter` - Filter for list prompts handlers
- `AddGetPromptFilter` - Filter for get prompt handlers
- `AddListResourcesFilter` - Filter for list resources handlers
- `AddReadResourceFilter` - Filter for read resource handlers
- `AddCompleteFilter` - Filter for completion handlers
- `AddSubscribeToResourcesFilter` - Filter for resource subscription handlers
- `AddUnsubscribeFromResourcesFilter` - Filter for resource unsubscription handlers
- `AddSetLoggingLevelFilter` - Filter for logging level handlers

## Filter Delegate Types

Each filter method uses strongly-typed delegate types for type safety:

```csharp
// Example delegate types
public delegate ValueTask<ListToolsResult> ListToolsHandler(RequestContext<ListToolsRequestParams> context, CancellationToken cancellationToken);
public delegate ValueTask<CallToolResult> CallToolHandler(RequestContext<CallToolRequestParams> context, CancellationToken cancellationToken);
// ... and so on for each handler type
```

## Usage

Filters are functions that take a handler and return a new handler, allowing you to wrap the original handler with additional functionality:

```csharp
services.AddMcpServer()
    .WithListToolsHandler(async (context, cancellationToken) =>
    {
        // Your base handler logic
        return new ListToolsResult { Tools = GetTools() };
    })
    .AddListToolsFilter(next => async (context, cancellationToken) =>
    {
        // Pre-processing logic
        Console.WriteLine("Before handler execution");

        var result = await next(context, cancellationToken);

        // Post-processing logic
        Console.WriteLine("After handler execution");
        return result;
    });
```

## Filter Execution Order

Filters are applied in **reverse order**, meaning the last filter added is the outermost (first to execute):

```csharp
services.AddMcpServer()
    .WithListToolsHandler(baseHandler)
    .AddListToolsFilter(filter1)  // Executes third (closest to handler)
    .AddListToolsFilter(filter2)  // Executes second
    .AddListToolsFilter(filter3); // Executes first (outermost)
```

Execution flow: `filter3 -> filter2 -> filter1 -> baseHandler -> filter1 -> filter2 -> filter3`

## Implementation Details

### Filter Pipeline Construction
- Filters are applied in reverse order, so the last filter added is the outermost (first to execute)
- The filter pipeline is built during server configuration in the respective `Configure` methods:
  - `ConfigureTools` - applies tool-related filters
  - `ConfigureResources` - applies resource-related filters
  - `ConfigurePrompts` - applies prompt-related filters
  - `ConfigureCompletion` - applies completion filters
  - `ConfigureLogging` - applies logging filters

### Helper Methods
The `McpServerFilters` class provides static helper methods for building filter pipelines:
- `BuildFilterPipeline<THandler>` - Builds a filter pipeline from a list of filters
- `WrapHandlerWithFilters<TRequest, TResult>` - Wraps a handler with filters

This allows for middleware-style composition where outer filters can perform actions before and after inner filters and the base handler.

## Common Use Cases

### Logging
```csharp
.AddListToolsFilter(next => async (context, cancellationToken) =>
{
    Console.WriteLine($"Processing request from {context.Meta.ProgressToken}");
    var result = await next(context, cancellationToken);
    Console.WriteLine($"Returning {result.Tools?.Count ?? 0} tools");
    return result;
});
```

### Error Handling
```csharp
.AddCallToolFilter(next => async (context, cancellationToken) =>
{
    try
    {
        return await next(context, cancellationToken);
    }
    catch (Exception ex)
    {
        return new CallToolResult
        {
            Content = new[] { new TextContent { Type = "text", Text = $"Error: {ex.Message}" } },
            IsError = true
        };
    }
});
```

### Performance Monitoring
```csharp
.AddListToolsFilter(next => async (context, cancellationToken) =>
{
    var stopwatch = Stopwatch.StartNew();
    var result = await next(context, cancellationToken);
    stopwatch.Stop();
    Console.WriteLine($"Handler took {stopwatch.ElapsedMilliseconds}ms");
    return result;
});
```

### Caching
```csharp
.AddListResourcesFilter(next => async (context, cancellationToken) =>
{
    var cacheKey = $"resources:{context.Params.Cursor}";
    if (cache.TryGetValue(cacheKey, out var cached))
        return cached;

    var result = await next(context, cancellationToken);
    cache.Set(cacheKey, result, TimeSpan.FromMinutes(5));
    return result;
});
```

## Migration Notes

### Breaking Changes
- The `McpServerHandlers.OverwriteWithSetHandlers` method has been removed
- Filter registration has been moved from `McpServerHandlers` to `McpServerOptions.Filters`
- Filter pipeline construction now happens in `McpServer.Configure*` methods instead of `McpServerHandlers`

### Code Changes Required
If you were previously accessing filter collections directly (which was an internal API), you should now:
1. Use the extension methods like `AddListToolsFilter()` instead of accessing internal filter collections
2. Access filters through `options.Filters.ListToolsFilters` instead of through `McpServerHandlers`

## Example

See `examples/FilterExample.cs` for a complete working example demonstrating multiple filters on different handler types.
