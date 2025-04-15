using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using ModelContextProtocol.AspNetCore.Tests.Utils;
using System.Net;
using System.Text;

namespace ModelContextProtocol.AspNetCore.Tests;

public class HttpStreamingIntegrationTests(ITestOutputHelper outputHelper) : KestrelInMemoryTest(outputHelper)
{
    [Fact]
    public async Task InitializeResultResponse_Includes_McpSessionIdHeader()
    {
        Builder.Services.AddMcpServer().WithHttpTransport();
        await using var app = Builder.Build();
        app.MapMcp();
        await app.StartAsync(TestContext.Current.CancellationToken);

        const string initializeRequest = """
            {"jsonrpc":"2.0","id":"1","method":"initialize","params":{"protocolVersion":"2025-03-26","capabilities":{},"clientInfo":{"name":"IntegrationTestClient","version":"1.0.0"}}}
            """;

        using var initializeRequestBody = new StringContent(initializeRequest, Encoding.UTF8, "application/json");
        var response = await HttpClient.PostAsync("", initializeRequestBody, TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var sessionId = Assert.Single(response.Headers.GetValues("mcp-session-id"));
    }
}
