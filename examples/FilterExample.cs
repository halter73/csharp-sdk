using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;

namespace ModelContextProtocol.Examples;

/// <summary>
/// Example demonstrating the new filter functionality for MCP server handlers.
/// This shows how to use the AddXXXFilter methods to add middleware-style filters
/// to handler pipelines.
/// </summary>
public class FilterExample
{
    public static void ConfigureFilters(IServiceCollection services)
    {
        services.AddMcpServer()
            // Configure a basic handler
            .WithListToolsHandler(async (context, cancellationToken) =>
            {
                return new ListToolsResult
                {
                    Tools = new[]
                    {
                        new Tool { Name = "example-tool", Description = "An example tool" }
                    }
                };
            })

            // Add logging filter (applied last, executes first)
            .AddListToolsFilter(next => async (context, cancellationToken) =>
            {
                Console.WriteLine($"[Filter 1] Before processing list tools request from {context.Meta.ProgressToken}");

                var result = await next(context, cancellationToken);

                Console.WriteLine($"[Filter 1] After processing, returning {result.Tools?.Count ?? 0} tools");
                return result;
            })

            // Add timing filter (applied second, executes second)
            .AddListToolsFilter(next => async (context, cancellationToken) =>
            {
                var stopwatch = System.Diagnostics.Stopwatch.StartNew();
                Console.WriteLine("[Filter 2] Starting timing measurement");

                var result = await next(context, cancellationToken);

                stopwatch.Stop();
                Console.WriteLine($"[Filter 2] Handler execution took {stopwatch.ElapsedMilliseconds}ms");
                return result;
            })

            // Add validation filter (applied first, executes last before handler)
            .AddListToolsFilter(next => async (context, cancellationToken) =>
            {
                Console.WriteLine("[Filter 3] Validating request");

                // Could add validation logic here
                if (cancellationToken.IsCancellationRequested)
                {
                    Console.WriteLine("[Filter 3] Request was cancelled");
                    throw new OperationCanceledException();
                }

                var result = await next(context, cancellationToken);
                Console.WriteLine("[Filter 3] Request validation completed");
                return result;
            })

            // Example of call tool filter with error handling
            .WithCallToolHandler(async (context, cancellationToken) =>
            {
                return new CallToolResult
                {
                    Content = new[]
                    {
                        new TextContent { Type = "text", Text = $"Tool {context.Params.Name} executed successfully" }
                    }
                };
            })
            .AddCallToolFilter(next => async (context, cancellationToken) =>
            {
                try
                {
                    Console.WriteLine($"[CallTool Filter] Calling tool: {context.Params.Name}");
                    var result = await next(context, cancellationToken);
                    Console.WriteLine($"[CallTool Filter] Tool call succeeded");
                    return result;
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[CallTool Filter] Tool call failed: {ex.Message}");
                    return new CallToolResult
                    {
                        Content = new[]
                        {
                            new TextContent { Type = "text", Text = $"Tool execution failed: {ex.Message}" }
                        },
                        IsError = true
                    };
                }
            })

            // Example of resource handler filter with caching
            .WithListResourcesHandler(async (context, cancellationToken) =>
            {
                return new ListResourcesResult
                {
                    Resources = new[]
                    {
                        new Resource { Uri = "file://example.txt", Name = "Example File", Description = "A sample resource" }
                    }
                };
            })
            .AddListResourcesFilter(next => async (context, cancellationToken) =>
            {
                // Simple caching example (in real usage, you'd use a proper cache)
                var cacheKey = $"resources:{context.Params.Cursor}";
                Console.WriteLine($"[Cache Filter] Checking cache for key: {cacheKey}");

                // In a real implementation, check cache here
                // For this example, just call through to the handler
                var result = await next(context, cancellationToken);

                Console.WriteLine($"[Cache Filter] Caching result with {result.Resources?.Count ?? 0} resources");
                // In a real implementation, store result in cache here

                return result;
            });
    }
}

/*
Expected execution order when ListToolsHandler is called:

1. Filter 3 (Validation) - "Validating request"
2. Filter 2 (Timing) - "Starting timing measurement"
3. Filter 1 (Logging) - "Before processing list tools request"
4. Base Handler - Executes the actual tool listing logic
5. Filter 1 (Logging) - "After processing, returning X tools"
6. Filter 2 (Timing) - "Handler execution took Xms"
7. Filter 3 (Validation) - "Request validation completed"

This demonstrates the reverse-order execution where the last added filter
is the outermost (first to execute) and the first added filter is the
innermost (closest to the base handler).
*/
