using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using ModelContextProtocol.AspNetCore.Tests.Utils;
using ModelContextProtocol.Protocol.Messages;
using ModelContextProtocol.Protocol.Types;
using ModelContextProtocol.Server;
using ModelContextProtocol.Utils.Json;
using System.ComponentModel;
using System.Net;
using System.Net.ServerSentEvents;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization.Metadata;

namespace ModelContextProtocol.AspNetCore.Tests;

public class HttpStreamingIntegrationTests(ITestOutputHelper outputHelper) : KestrelInMemoryTest(outputHelper)
{
    private const string _initializeRequest = """
        {"jsonrpc":"2.0","id":1,"method":"initialize","params":{"protocolVersion":"2025-03-26","capabilities":{},"clientInfo":{"name":"IntegrationTestClient","version":"1.0.0"}}}
        """;
    private const string _echoRequest = """
        {"jsonrpc":"2.0","id":2,"method":"tools/call","params":{"name":"echo","arguments":{"message":"Hello world!"}}}
        """;

    [Fact]
    public async Task InitializeRequestResponse_Includes_McpSessionIdHeader()
    {
        Builder.Services.AddMcpServer().WithHttpTransport();
        await using var app = Builder.Build();
        app.MapMcp();
        await app.StartAsync(TestContext.Current.CancellationToken);

        using var response = await HttpClient.PostAsync("", JsonContent(_initializeRequest), TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var sessionId = Assert.Single(response.Headers.GetValues("mcp-session-id"));
    }

    [Fact]
    public async Task InitializeRequest_Matches_CustomRoute()
    {
        Builder.Services.AddMcpServer().WithHttpTransport();
        await using var app = Builder.Build();
        app.MapMcp("/mcp");
        await app.StartAsync(TestContext.Current.CancellationToken);

        using var response = await HttpClient.PostAsync("/mcp", JsonContent(_initializeRequest), TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task SingleJsonRpcRequest_Completes_SseResponse()
    {
        Builder.Services.AddMcpServer(ConfigureServerInfo).WithHttpTransport();
        await using var app = Builder.Build();
        app.MapMcp();
        await app.StartAsync(TestContext.Current.CancellationToken);

        // This should work with the default HttpCompletionOption.ResponseContentRead setting.
        using var response = await HttpClient.PostAsync("", JsonContent(_initializeRequest), TestContext.Current.CancellationToken);
        var jsonRpcResponse = await AssertSingleSseResponseAsync(response);
        AssertServerInfo(jsonRpcResponse.Result);
    }

    [Fact]
    public async Task BatchedJsonRpcRequests_Completes_SseResponse()
    {
        Builder.Services.AddMcpServer(ConfigureServerInfo).WithHttpTransport();
        Builder.Services.AddSingleton(McpServerTool.Create(Echo));
        await using var app = Builder.Build();
        app.MapMcp();
        await app.StartAsync(TestContext.Current.CancellationToken);

        using var response = await HttpClient.PostAsync("", JsonContent($"[{_initializeRequest},{_echoRequest}]"), TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var eventCount = 0;
        await foreach (SseItem<string> sseEvent in ReadSseAsync(response.Content).ConfigureAwait(false))
        {
            var jsonRpcResponse = JsonSerializer.Deserialize(sseEvent.Data, GetJsonTypeInfo<JsonRpcResponse>());
            Assert.NotNull(jsonRpcResponse);
            var responseId = Assert.IsType<long>(jsonRpcResponse.Id.Id);

            switch (responseId)
            {
                case 1:
                    AssertServerInfo(jsonRpcResponse.Result);
                    break;
                case 2:
                    AssertEchoResponse(jsonRpcResponse.Result);
                    break;
                default:
                    throw new Exception($"Unexpected response ID: {jsonRpcResponse.Id}");
            };

            eventCount++;
        }

        Assert.Equal(2, eventCount);
    }

    [Fact]
    public async Task MultipleSerialJsonRpcRequests_Complete_OneAtATime()
    {
        Builder.Services.AddMcpServer(ConfigureServerInfo).WithHttpTransport();
        Builder.Services.AddSingleton(McpServerTool.Create(Echo));
        await using var app = Builder.Build();
        app.MapMcp();
        await app.StartAsync(TestContext.Current.CancellationToken);

        using var initializeResponse = await HttpClient.PostAsync("", JsonContent(_initializeRequest), TestContext.Current.CancellationToken);
        var initializeJsonRpcResponse = await AssertSingleSseResponseAsync(initializeResponse);
        AssertServerInfo(initializeJsonRpcResponse.Result);

        var sessionId = Assert.Single(initializeResponse.Headers.GetValues("mcp-session-id"));
        using var echoToolRequest = new HttpRequestMessage(HttpMethod.Post, "")
        {
            Content = JsonContent(_echoRequest),
            Headers =
            {
                { "mcp-session-id", sessionId },
            },
        };

        using var echoToolResponse = await HttpClient.SendAsync(echoToolRequest, TestContext.Current.CancellationToken);
        var rpcResponse = await AssertSingleSseResponseAsync(echoToolResponse);
        AssertEchoResponse(rpcResponse.Result);
    }

    [McpServerTool(Name = "echo"), Description("Echoes the input back to the client.")]
    private static string Echo(string message) => message;

    private static void ConfigureServerInfo(McpServerOptions options)
    {
        options.ServerInfo = new Implementation
        {
            Name = "TestServer",
            Version = "73",
        };
    }

    private static T AssertType<T>(JsonNode? jsonNode)
    {
        var type = JsonSerializer.Deserialize<T>(jsonNode, GetJsonTypeInfo<T>());
        Assert.NotNull(type);
        return type;
    }

    private static InitializeResult AssertServerInfo(JsonNode? result)
    {
        var initializeResult = AssertType<InitializeResult>(result);
        Assert.Equal("TestServer", initializeResult.ServerInfo.Name);
        Assert.Equal("73", initializeResult.ServerInfo.Version);
        return initializeResult;
    }

    private static CallToolResponse AssertEchoResponse(JsonNode? result)
    {
        var callToolResponse = AssertType<CallToolResponse>(result);
        var callToolContent = Assert.Single(callToolResponse.Content);
        Assert.Equal("text", callToolContent.Type);
        Assert.Equal("Hello world!", callToolContent.Text);
        return callToolResponse;
    }

    private static StringContent JsonContent(string json) => new StringContent(json, Encoding.UTF8, "application/json");
    private static JsonTypeInfo<T> GetJsonTypeInfo<T>() => (JsonTypeInfo<T>)McpJsonUtilities.DefaultOptions.GetTypeInfo(typeof(T));

    private static async IAsyncEnumerable<SseItem<string>> ReadSseAsync(HttpContent responseContent)
    {
        var responseStream = await responseContent.ReadAsStreamAsync(TestContext.Current.CancellationToken);
        await foreach (var sseItem in SseParser.Create(responseStream).EnumerateAsync(TestContext.Current.CancellationToken))
        {
            Assert.Equal("message", sseItem.EventType);
            yield return sseItem;
        }
    }

    private static async Task<JsonRpcResponse> AssertSingleSseResponseAsync(HttpResponseMessage response)
    {
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("text/event-stream", response.Content.Headers.ContentType?.MediaType);

        var sseItem = Assert.Single(await ReadSseAsync(response.Content).ToListAsync(TestContext.Current.CancellationToken));
        var jsonRpcResponse = JsonSerializer.Deserialize(sseItem.Data, GetJsonTypeInfo<JsonRpcResponse>());

        Assert.NotNull(jsonRpcResponse);
        return jsonRpcResponse;
    }
}
