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

        using var initializeRequestBody = new StringContent(initializeRequest, Encoding.UTF8, "application/json");
        using var postRequestMessage = new HttpRequestMessage(HttpMethod.Post, "")
        {
            Content = initializeRequestBody,
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

        using var initializeRequestBody = new StringContent(initializeRequest, Encoding.UTF8, "application/json");
        using var postRequestMessage = new HttpRequestMessage(HttpMethod.Post, "/mcp")
        {
            Content = initializeRequestBody,
        };
        using var response = await HttpClient.SendAsync(postRequestMessage, HttpCompletionOption.ResponseHeadersRead, TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task SingleRequest_CompletesHttpResponse_AfterSendingJsonRpcResponse()
    {
        Builder.Services.AddMcpServer().WithHttpTransport();
        await using var app = Builder.Build();
        app.MapMcp();
        await app.StartAsync(TestContext.Current.CancellationToken);

        using var initializeRequestBody = new StringContent(initializeRequest, Encoding.UTF8, "application/json");
        // This should work with the default HttpCompletionOption.ResponseContentRead setting.
        using var response = await HttpClient.PostAsync("", initializeRequestBody, TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task BatchedRequest_CompletesResponse_AfterSendingAllJsonRpcResponses()
    {
        Builder.Services.AddMcpServer(options =>
        {
            options.ServerInfo = new()
            {
                Name = "TestServer",
                Version = "73",
            };
        }).WithHttpTransport();
        Builder.Services.AddSingleton(McpServerTool.Create(Echo));
        await using var app = Builder.Build();
        app.MapMcp();
        await app.StartAsync(TestContext.Current.CancellationToken);

        using var batchedRequestBody = new StringContent($"[{initializeRequest},{echoToolRequest}]", Encoding.UTF8, "application/json");
        using var response = await HttpClient.PostAsync("", batchedRequestBody, TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var responseBodyStream = await response.Content.ReadAsStreamAsync(TestContext.Current.CancellationToken);
        var sseAsyncEnumerable = SseParser.Create(responseBodyStream).EnumerateAsync(TestContext.Current.CancellationToken);

        var eventCount = 0;
        await foreach (SseItem<string> sseEvent in sseAsyncEnumerable.ConfigureAwait(false))
        {
            Assert.Equal("message", sseEvent.EventType);

            var jsonRpcResponse = JsonSerializer.Deserialize(sseEvent.Data, GetJsonTypeInfo<JsonRpcResponse>());
            Assert.NotNull(jsonRpcResponse);
            var responseId = Assert.IsType<long>(jsonRpcResponse.Id.Id);

            switch (responseId)
            {
                case 1:
                    var initializeResult = JsonSerializer.Deserialize(jsonRpcResponse.Result, GetJsonTypeInfo<InitializeResult>());
                    Assert.Equal("TestServer", initializeResult?.ServerInfo.Name);
                    Assert.Equal("73", initializeResult?.ServerInfo.Version);
                    break;
                case 2:
                    var callToolResponse = JsonSerializer.Deserialize(jsonRpcResponse.Result, GetJsonTypeInfo<CallToolResponse>());
                    Assert.NotNull(callToolResponse);
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

    [McpServerTool(Name = "echo"), Description("Echoes the input back to the client.")]
    public static string Echo(string message) => message;

    private static JsonTypeInfo<T> GetJsonTypeInfo<T>() =>
        (JsonTypeInfo<T>)McpJsonUtilities.DefaultOptions.GetTypeInfo(typeof(T));
}
