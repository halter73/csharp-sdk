# MCP Server Handler Filters

This document describes the new filter functionality added to the MCP Server, which allows you to add middleware-style filters to handler pipelines.

## Overview

For each handler type in `McpServerHandlers.cs` that has a corresponding `WithXXXHandler` method in `McpServerBuilderExtensions.cs`, there is now a corresponding `UseXXXFilter` method that allows you to add filters to the handler pipeline.

## Available Filter Methods

The following filter methods are available:

- `UseListResourceTemplatesFilter` - Filter for list resource templates handlers
- `UseListToolsFilter` - Filter for list tools handlers  
- `UseCallToolFilter` - Filter for call tool handlers
- `UseListPromptsFilter` - Filter for list prompts handlers
- `UseGetPromptFilter` - Filter for get prompt handlers
- `UseListResourcesFilter` - Filter for list resources handlers
- `UseReadResourceFilter` - Filter for read resource handlers
- `UseCompleteFilter` - Filter for completion handlers
- `UseSubscribeToResourcesFilter` - Filter for resource subscription handlers
- `UseUnsubscribeFromResourcesFilter` - Filter for resource unsubscription handlers
- `UseSetLoggingLevelFilter` - Filter for logging level handlers

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
    .UseListToolsFilter(next => async (context, cancellationToken) =>
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
    .UseListToolsFilter(filter1)  // Executes third (closest to handler)
    .UseListToolsFilter(filter2)  // Executes second  
    .UseListToolsFilter(filter3); // Executes first (outermost)
```

Execution flow: `filter3 -> filter2 -> filter1 -> baseHandler -> filter1 -> filter2 -> filter3`

This allows for middleware-style composition where outer filters can perform actions before and after inner filters and the base handler.

## Common Use Cases

### Logging
```csharp
.UseListToolsFilter(next => async (context, cancellationToken) =>
{
    Console.WriteLine($"Processing request from {context.Meta.ProgressToken}");
    var result = await next(context, cancellationToken);
    Console.WriteLine($"Returning {result.Tools?.Count ?? 0} tools");
    return result;
});
```

### Error Handling
```csharp
.UseCallToolFilter(next => async (context, cancellationToken) =>
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
.UseListToolsFilter(next => async (context, cancellationToken) =>
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
.UseListResourcesFilter(next => async (context, cancellationToken) =>
{
    var cacheKey = $"resources:{context.Params.Cursor}";
    if (cache.TryGetValue(cacheKey, out var cached))
        return cached;
        
    var result = await next(context, cancellationToken);
    cache.Set(cacheKey, result, TimeSpan.FromMinutes(5));
    return result;
});
```

## Implementation Details

- Filters are stored in private `List<Func<THandler, THandler>>` fields in `McpServerHandlers`
- The filter pipeline is built when `OverwriteWithSetHandlers` is called
- Filters are applied even if no base handler is set, but the capability won't be created without a base handler
- The filter system is fully type-safe and uses the same delegate types as the handlers

## Example

See `examples/FilterExample.cs` for a complete working example demonstrating multiple filters on different handler types.
