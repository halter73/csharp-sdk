﻿using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Json;
using Microsoft.Extensions.DependencyInjection;
using ModelContextProtocol.AspNetCore.Tests.Utils;
using ModelContextProtocol.Client;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;
using System.Text.Json;
using System.Text.Json.Serialization.Metadata;

namespace ModelContextProtocol.AspNetCore.Tests;

public class StreamableHttpClientConformanceTests(ITestOutputHelper outputHelper) : KestrelInMemoryTest(outputHelper), IAsyncDisposable
{
    private WebApplication? _app;
    private static readonly List<string> DeleteRequests = new();

    private async Task StartAsync()
    {
        DeleteRequests.Clear();
        
        Builder.Services.Configure<JsonOptions>(options =>
        {
            options.SerializerOptions.TypeInfoResolverChain.Add(McpJsonUtilities.DefaultOptions.TypeInfoResolver!);
        });
        _app = Builder.Build();

        var echoTool = McpServerTool.Create(Echo, new()
        {
            Services = _app.Services,
        });

        _app.MapPost("/mcp", (JsonRpcMessage message, HttpContext context) =>
        {
            if (message is not JsonRpcRequest request)
            {
                // Ignore all non-request notifications.
                return Results.Accepted();
            }

            if (request.Method == "initialize")
            {
                var response = Results.Json(new JsonRpcResponse
                {
                    Id = request.Id,
                    Result = JsonSerializer.SerializeToNode(new InitializeResult
                    {
                        ProtocolVersion = "2024-11-05",
                        Capabilities = new()
                        {
                            Tools = new(),
                        },
                        ServerInfo = new Implementation
                        {
                            Name = "my-mcp",
                            Version = "0.0.1",
                        },
                    }, McpJsonUtilities.DefaultOptions)
                });

                // Add a session ID to the response to enable session tracking
                context.Response.Headers.Append("mcp-session-id", "test-session-123");
                return response;
            }

            if (request.Method == "tools/list")
            {
                return Results.Json(new JsonRpcResponse
                {
                    Id = request.Id,
                    Result = JsonSerializer.SerializeToNode(new ListToolsResult
                    {
                        Tools = [echoTool.ProtocolTool]
                    }, McpJsonUtilities.DefaultOptions),
                });
            }

            if (request.Method == "tools/call")
            {
                var parameters = JsonSerializer.Deserialize(request.Params, GetJsonTypeInfo<CallToolRequestParams>());
                Assert.NotNull(parameters?.Arguments);

                return Results.Json(new JsonRpcResponse
                {
                    Id = request.Id,
                    Result = JsonSerializer.SerializeToNode(new CallToolResponse()
                    {
                        Content = [new() { Text = parameters.Arguments["message"].ToString() }],
                    }, McpJsonUtilities.DefaultOptions),
                });
            }

            throw new Exception("Unexpected message!");
        });

        // Add a DELETE endpoint to track if DELETE requests are sent
        _app.MapDelete("/mcp", (HttpContext context) =>
        {
            var sessionId = context.Request.Headers["mcp-session-id"].ToString();
            DeleteRequests.Add(sessionId);
            return Results.Ok();
        });

        await _app.StartAsync(TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task CanCallToolOnSessionlessStreamableHttpServer()
    {
        await StartAsync();

        await using var transport = new SseClientTransport(new()
        {
            Endpoint = new("http://localhost/mcp"),
            TransportMode = HttpTransportMode.StreamableHttp,
        }, HttpClient, LoggerFactory);

        await using var client = await McpClientFactory.CreateAsync(transport, loggerFactory: LoggerFactory, cancellationToken: TestContext.Current.CancellationToken);
        var tools = await client.ListToolsAsync(cancellationToken: TestContext.Current.CancellationToken);

        var echoTool = Assert.Single(tools);
        Assert.Equal("echo", echoTool.Name);
        await CallEchoAndValidateAsync(echoTool);
    }


    [Fact]
    public async Task CanCallToolConcurrently()
    {
        await StartAsync();

        await using var transport = new SseClientTransport(new()
        {
            Endpoint = new("http://localhost/mcp"),
            TransportMode = HttpTransportMode.StreamableHttp,
        }, HttpClient, LoggerFactory);

        await using var client = await McpClientFactory.CreateAsync(transport, loggerFactory: LoggerFactory, cancellationToken: TestContext.Current.CancellationToken);
        var tools = await client.ListToolsAsync(cancellationToken: TestContext.Current.CancellationToken);

        var echoTool = Assert.Single(tools);
        Assert.Equal("echo", echoTool.Name);

        var echoTasks = new Task[100];
        for (int i = 0; i < echoTasks.Length; i++)
        {
            echoTasks[i] = CallEchoAndValidateAsync(echoTool);
        }

        await Task.WhenAll(echoTasks);
    }

    [Fact]
    public async Task SendsDeleteRequestOnDispose()
    {
        await StartAsync();

        await using var transport = new SseClientTransport(new()
        {
            Endpoint = new("http://localhost/mcp"),
            TransportMode = HttpTransportMode.StreamableHttp,
        }, HttpClient, LoggerFactory);

        await using var client = await McpClientFactory.CreateAsync(transport, loggerFactory: LoggerFactory, cancellationToken: TestContext.Current.CancellationToken);
        
        // Make sure we have a session established
        var tools = await client.ListToolsAsync(cancellationToken: TestContext.Current.CancellationToken);
        var echoTool = Assert.Single(tools);
        Assert.Equal("echo", echoTool.Name);

        // Clear any previous DELETE requests
        DeleteRequests.Clear();

        // Dispose should trigger DELETE request
        await client.DisposeAsync();

        // Verify DELETE request was sent with correct session ID
        Assert.Single(DeleteRequests);
        Assert.Equal("test-session-123", DeleteRequests[0]);
    }

    private static async Task CallEchoAndValidateAsync(McpClientTool echoTool)
    {
        var response = await echoTool.CallAsync(new Dictionary<string, object?>() { ["message"] = "Hello world!" }, cancellationToken: TestContext.Current.CancellationToken);
        Assert.NotNull(response);
        var content = Assert.Single(response.Content);
        Assert.Equal("text", content.Type);
        Assert.Equal("Hello world!", content.Text);
    }

    public async ValueTask DisposeAsync()
    {
        if (_app is not null)
        {
            await _app.DisposeAsync();
        }
        base.Dispose();
    }

    private static JsonTypeInfo<T> GetJsonTypeInfo<T>() => (JsonTypeInfo<T>)McpJsonUtilities.DefaultOptions.GetTypeInfo(typeof(T));

    [McpServerTool(Name = "echo")]
    private static string Echo(string message)
    {
        return message;
    }
}
