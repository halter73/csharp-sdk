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
    const string initializeRequest = """
        {"jsonrpc":"2.0","id":1,"method":"initialize","params":{"protocolVersion":"2025-03-26","capabilities":{},"clientInfo":{"name":"IntegrationTestClient","version":"1.0.0"}}}
        """;

    const string echoToolRequest = """
        {"jsonrpc":"2.0","id":2,"method":"tools/call","params":{"name":"echo","arguments":{"message":"Hello world!"}}}
        """;

    [Fact]
    public async Task InitializeRequestResponse_Includes_McpSessionIdHeader()
    {
        Builder.Services.AddMcpServer().WithHttpTransport();
        await using var app = Builder.Build();
        app.MapMcp();
        await app.StartAsync(TestContext.Current.CancellationToken);

        using var postRequestMessage = new HttpRequestMessage(HttpMethod.Post, "")
        {
            Content = new StringContent(initializeRequest, Encoding.UTF8, "application/json"),
        };
        using var response = await HttpClient.SendAsync(postRequestMessage, HttpCompletionOption.ResponseHeadersRead, TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var sessionId = Assert.Single(response.Headers.GetValues("mcp-session-id"));
    }

    [Fact]
    public async Task CustomRoute_Works()
    {
        Builder.Services.AddMcpServer().WithHttpTransport();
        await using var app = Builder.Build();
        app.MapMcp("/mcp");
        await app.StartAsync(TestContext.Current.CancellationToken);

        using var postRequestMessage = new HttpRequestMessage(HttpMethod.Post, "/mcp")
        {
            Content = new StringContent(initializeRequest, Encoding.UTF8, "application/json"),
        };
        using var response = await HttpClient.SendAsync(postRequestMessage, HttpCompletionOption.ResponseHeadersRead, TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task SingleRequest_CompletesHttpResponse_AfterSendingJsonRpcResponse()
    {
        Builder.Services.AddMcpServer(ConfigureServerInfo).WithHttpTransport();
        await using var app = Builder.Build();
        app.MapMcp();
        await app.StartAsync(TestContext.Current.CancellationToken);

        using var initializeRequestBody = new StringContent(initializeRequest, Encoding.UTF8, "application/json");
        // This should work with the default HttpCompletionOption.ResponseContentRead setting.
        using var response = await HttpClient.PostAsync("", initializeRequestBody, TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var sseItem = Assert.Single(await ReadSseAsync(response.Content).ToListAsync(TestContext.Current.CancellationToken));
        var jsonRpcResponse = JsonSerializer.Deserialize(sseItem.Data, GetJsonTypeInfo<JsonRpcResponse>());
        Assert.NotNull(jsonRpcResponse);
        AssertServerInfo(jsonRpcResponse.Result);
    }

    [Fact]
    public async Task BatchedRequest_CompletesResponse_AfterSendingAllJsonRpcResponses()
    {
        Builder.Services.AddMcpServer(ConfigureServerInfo).WithHttpTransport();
        Builder.Services.AddSingleton(McpServerTool.Create(Echo));
        await using var app = Builder.Build();
        app.MapMcp();
        await app.StartAsync(TestContext.Current.CancellationToken);

        using var batchedRequestBody = new StringContent($"[{initializeRequest},{echoToolRequest}]", Encoding.UTF8, "application/json");
        using var response = await HttpClient.PostAsync("", batchedRequestBody, TestContext.Current.CancellationToken);
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
                    var callToolResponse = AssertType<CallToolResponse>(jsonRpcResponse.Result);
                    var callToolContent = Assert.Single(callToolResponse.Content);
                    Assert.Equal("text", callToolContent.Type);
                    Assert.Equal("Hello world!", callToolContent.Text);
                    break;
                default:
                    throw new Exception($"Unexpected response ID: {jsonRpcResponse.Id}");
            };

            eventCount++;
        }

        Assert.Equal(2, eventCount);
    }

    [Fact]
    public async Task DoubleInitializeRequest_Is_Rejected()
    {
        Builder.Services.AddMcpServer().WithHttpTransport();
        await using var app = Builder.Build();
        app.MapMcp();
        await app.StartAsync(TestContext.Current.CancellationToken);

        using var firstInitializeMessage = new HttpRequestMessage(HttpMethod.Post, "")
        {
            Content = new StringContent(initializeRequest, Encoding.UTF8, "application/json"),
        };
        using var firstInitializeResponse = await HttpClient.SendAsync(firstInitializeMessage, HttpCompletionOption.ResponseHeadersRead, TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.OK, firstInitializeResponse.StatusCode);
        var sessionId = Assert.Single(firstInitializeResponse.Headers.GetValues("mcp-session-id"));

        using var secondInitializeMessage = new HttpRequestMessage(HttpMethod.Post, "")
        {
            Content = new StringContent(initializeRequest, Encoding.UTF8, "application/json"),
            Headers =
            {
                { "mcp-session-id", sessionId },
            },
        };

        using var response = await HttpClient.SendAsync(secondInitializeMessage, HttpCompletionOption.ResponseContentRead, TestContext.Current.CancellationToken);
        var responseBody = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
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

    private static void AssertServerInfo(JsonNode? result)
    {
        var initializeResult = AssertType<InitializeResult>(result);
        Assert.Equal("TestServer", initializeResult.ServerInfo.Name);
        Assert.Equal("73", initializeResult.ServerInfo.Version);
    }

    private static async IAsyncEnumerable<SseItem<string>> ReadSseAsync(HttpContent responseContent)
    {
        var responseStream = await responseContent.ReadAsStreamAsync(TestContext.Current.CancellationToken);
        await foreach (var sseItem in SseParser.Create(responseStream).EnumerateAsync(TestContext.Current.CancellationToken))
        {
            Assert.Equal("message", sseItem.EventType);
            yield return sseItem;
        }
    }

    private static JsonTypeInfo<T> GetJsonTypeInfo<T>() =>
        (JsonTypeInfo<T>)McpJsonUtilities.DefaultOptions.GetTypeInfo(typeof(T));
}
