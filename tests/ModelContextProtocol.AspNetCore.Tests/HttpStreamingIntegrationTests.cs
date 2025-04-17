using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using ModelContextProtocol.AspNetCore.Tests.Utils;
using ModelContextProtocol.Server;
using System.ComponentModel;
using System.Net;
using System.Text;

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
        var response = await HttpClient.SendAsync(postRequestMessage, HttpCompletionOption.ResponseHeadersRead, TestContext.Current.CancellationToken);
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
        var response = await HttpClient.SendAsync(postRequestMessage, HttpCompletionOption.ResponseHeadersRead, TestContext.Current.CancellationToken);
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
        var response = await HttpClient.PostAsync("", initializeRequestBody, TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task BatchedRequest_CompletesResponse_AfterSendingAllJsonRpcResponses()
    {
        Builder.Services.AddMcpServer().WithHttpTransport();
        Builder.Services.AddSingleton(McpServerTool.Create(Echo));
        await using var app = Builder.Build();
        app.MapMcp();
        await app.StartAsync(TestContext.Current.CancellationToken);

        using var batchedRequestBody = new StringContent($"[{initializeRequest},{echoToolRequest}]", Encoding.UTF8, "application/json");
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        cts.CancelAfter(TimeSpan.FromSeconds(5));
        var response = await HttpClient.PostAsync("", batchedRequestBody, cts.Token);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [McpServerTool(Name = "echo"), Description("Echoes the input back to the client.")]
    public static string Echo(string message) => message;
}
