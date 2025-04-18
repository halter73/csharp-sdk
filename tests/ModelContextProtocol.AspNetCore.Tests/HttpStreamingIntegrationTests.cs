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
    private static string InitializeRequest => """
        {"jsonrpc":"2.0","id":1,"method":"initialize","params":{"protocolVersion":"2025-03-26","capabilities":{},"clientInfo":{"name":"IntegrationTestClient","version":"1.0.0"}}}
        """;

    private long _lastRequestId = 1;
    private string EchoRequest
    {
        get
        {
            var id = Interlocked.Increment(ref _lastRequestId);
            return $$$$"""
                {"jsonrpc":"2.0","id":{{{{id}}}},"method":"tools/call","params":{"name":"echo","arguments":{"message":"Hello world! ({{{{id}}}})"}}}
                """;
        }
    }

    [Fact]
    public async Task InitialPostResponse_Includes_McpSessionIdHeader()
    {
        Builder.Services.AddMcpServer().WithHttpTransport();
        await using var app = Builder.Build();
        app.MapMcp();
        await app.StartAsync(TestContext.Current.CancellationToken);

        using var response = await HttpClient.PostAsync("", JsonContent(InitializeRequest), TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Single(response.Headers.GetValues("mcp-session-id"));
        Assert.Equal("text/event-stream", Assert.Single(response.Content.Headers.GetValues("content-type")));
    }

    [Fact]
    public async Task PostRequest_IsRejected_WithoutJsonContentType()
    {
        Builder.Services.AddMcpServer().WithHttpTransport();
        await using var app = Builder.Build();
        app.MapMcp();
        await app.StartAsync(TestContext.Current.CancellationToken);

        using var response = await HttpClient.PostAsync("", new StringContent(InitializeRequest, Encoding.UTF8, "text/javascript"), TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.UnsupportedMediaType, response.StatusCode);
    }

    [Fact]
    public async Task InitializeRequest_Matches_CustomRoute()
    {
        Builder.Services.AddMcpServer().WithHttpTransport();
        await using var app = Builder.Build();
        app.MapMcp("/mcp");
        await app.StartAsync(TestContext.Current.CancellationToken);

        using var response = await HttpClient.PostAsync("/mcp", JsonContent(InitializeRequest), TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task SingleJsonRpcRequest_Completes_WithSseResponse()
    {
        Builder.Services.AddMcpServer(ConfigureServerInfo).WithHttpTransport();
        await using var app = Builder.Build();
        app.MapMcp();
        await app.StartAsync(TestContext.Current.CancellationToken);

        // This should work with the default HttpCompletionOption.ResponseContentRead setting.
        using var response = await HttpClient.PostAsync("", JsonContent(InitializeRequest), TestContext.Current.CancellationToken);
        var jsonRpcResponse = await AssertSingleSseResponseAsync(response);
        AssertServerInfo(jsonRpcResponse);
    }

    [Fact]
    public async Task BatchedJsonRpcRequests_Completes_WithSseResponse()
    {
        Builder.Services.AddMcpServer(ConfigureServerInfo).WithHttpTransport();
        Builder.Services.AddSingleton(McpServerTool.Create(Echo));
        await using var app = Builder.Build();
        app.MapMcp();
        await app.StartAsync(TestContext.Current.CancellationToken);

        using var response = await HttpClient.PostAsync("", JsonContent($"[{InitializeRequest},{EchoRequest}]"), TestContext.Current.CancellationToken);
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
                    AssertServerInfo(jsonRpcResponse);
                    break;
                case 2:
                    AssertEchoResponse(jsonRpcResponse);
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

        using var initializeResponse = await HttpClient.PostAsync("", JsonContent(InitializeRequest), TestContext.Current.CancellationToken);
        var initializeJsonRpcResponse = await AssertSingleSseResponseAsync(initializeResponse);
        AssertServerInfo(initializeJsonRpcResponse);

        var sessionId = Assert.Single(initializeResponse.Headers.GetValues("mcp-session-id"));
        await CallEchoAndValidateAsync(sessionId);
    }

    [Fact]
    public async Task MultipleConcurrentJsonRpcRequests_Complete_InParallel()
    {
        Builder.Services.AddMcpServer(ConfigureServerInfo).WithHttpTransport();
        Builder.Services.AddSingleton(McpServerTool.Create(Echo));
        await using var app = Builder.Build();
        app.MapMcp();
        await app.StartAsync(TestContext.Current.CancellationToken);

        using var initializeResponse = await HttpClient.PostAsync("", JsonContent(InitializeRequest), TestContext.Current.CancellationToken);
        var initializeJsonRpcResponse = await AssertSingleSseResponseAsync(initializeResponse);
        AssertServerInfo(initializeJsonRpcResponse);

        var sessionId = Assert.Single(initializeResponse.Headers.GetValues("mcp-session-id"));
        var echoTask = new Task[100];

        for (int i = 0; i < echoTask.Length; i++)
        {
            echoTask[i] = CallEchoAndValidateAsync(sessionId);
        }

        await Task.WhenAll(echoTask);
    }

    [McpServerTool(Name = "echo"), Description("Echoes the input back to the client.")]
    private static async Task<string> Echo(string message)
    {
        // McpSession.ProcessMessagesAsync() already yields before calling any handlers, but this makes it even
        // more explicit that we're not relying on synchronous execution of the tool.
        await Task.Yield();
        return message;
    }

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

    private static InitializeResult AssertServerInfo(JsonRpcResponse rpcResponse)
    {
        var initializeResult = AssertType<InitializeResult>(rpcResponse.Result);
        Assert.Equal("TestServer", initializeResult.ServerInfo.Name);
        Assert.Equal("73", initializeResult.ServerInfo.Version);
        return initializeResult;
    }

    private static CallToolResponse AssertEchoResponse(JsonRpcResponse rpcResponse)
    {
        var callToolResponse = AssertType<CallToolResponse>(rpcResponse.Result);
        var callToolContent = Assert.Single(callToolResponse.Content);
        Assert.Equal("text", callToolContent.Type);
        Assert.Equal($"Hello world! ({rpcResponse.Id})", callToolContent.Text);
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

    private async Task CallEchoAndValidateAsync(string sessionId)
    {
        using var echoRequest = new HttpRequestMessage(HttpMethod.Post, "")
        {
            Content = JsonContent(EchoRequest),
            Headers =
            {
                { "mcp-session-id", sessionId },
            },
        };

        using var echoResponse = await HttpClient.SendAsync(echoRequest, TestContext.Current.CancellationToken);
        var rpcResponse = await AssertSingleSseResponseAsync(echoResponse);
        AssertEchoResponse(rpcResponse);
    }
}
