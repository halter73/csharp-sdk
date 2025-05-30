using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using ModelContextProtocol.Client;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;

namespace ModelContextProtocol.AspNetCore.Tests;

public class MapMcpStreamableHttpTests(ITestOutputHelper outputHelper) : MapMcpTests(outputHelper)
{
    protected override bool UseStreamableHttp => true;
    protected override bool Stateless => false;

    [Theory]
    [InlineData("/a", "/a")]
    [InlineData("/a", "/a/")]
    [InlineData("/a/", "/a/")]
    [InlineData("/a/", "/a")]
    [InlineData("/a/b", "/a/b")]
    public async Task CanConnect_WithMcpClient_AfterCustomizingRoute(string routePattern, string requestPath)
    {
        Builder.Services.AddMcpServer(options =>
        {
            options.ServerInfo = new()
            {
                Name = "TestCustomRouteServer",
                Version = "1.0.0",
            };
        }).WithHttpTransport(ConfigureStateless);
        await using var app = Builder.Build();

        app.MapMcp(routePattern);

        await app.StartAsync(TestContext.Current.CancellationToken);

        var mcpClient = await ConnectAsync(requestPath);

        Assert.Equal("TestCustomRouteServer", mcpClient.ServerInfo.Name);
    }

    [Fact]
    public async Task StreamableHttpMode_Works_WithRootEndpoint()
    {
        Builder.Services.AddMcpServer(options =>
        {
            options.ServerInfo = new()
            {
                Name = "StreamableHttpTestServer",
                Version = "1.0.0",
            };
        }).WithHttpTransport(ConfigureStateless);
        await using var app = Builder.Build();

        app.MapMcp();

        await app.StartAsync(TestContext.Current.CancellationToken);

        await using var mcpClient = await ConnectAsync("/", new()
        {
            Endpoint = new Uri("http://localhost/"),
            TransportMode = HttpTransportMode.AutoDetect
        });

        Assert.Equal("StreamableHttpTestServer", mcpClient.ServerInfo.Name);
    }

    [Fact]
    public async Task AutoDetectMode_Works_WithRootEndpoint()
    {
        Builder.Services.AddMcpServer(options =>
        {
            options.ServerInfo = new()
            {
                Name = "AutoDetectTestServer",
                Version = "1.0.0",
            };
        }).WithHttpTransport(ConfigureStateless);
        await using var app = Builder.Build();

        app.MapMcp();

        await app.StartAsync(TestContext.Current.CancellationToken);

        await using var mcpClient = await ConnectAsync("/", new()
        {
            Endpoint = new Uri("http://localhost/"),
            TransportMode = HttpTransportMode.AutoDetect
        });

        Assert.Equal("AutoDetectTestServer", mcpClient.ServerInfo.Name);
    }

    [Fact]
    public async Task AutoDetectMode_Works_WithSseEndpoint()
    {
        Assert.SkipWhen(Stateless, "SSE endpoint is disabled in stateless mode.");

        Builder.Services.AddMcpServer(options =>
        {
            options.ServerInfo = new()
            {
                Name = "AutoDetectSseTestServer",
                Version = "1.0.0",
            };
        }).WithHttpTransport(ConfigureStateless);
        await using var app = Builder.Build();

        app.MapMcp();

        await app.StartAsync(TestContext.Current.CancellationToken);

        await using var mcpClient = await ConnectAsync("/sse", new()
        {
            Endpoint = new Uri("http://localhost/sse"),
            TransportMode = HttpTransportMode.AutoDetect
        });

        Assert.Equal("AutoDetectSseTestServer", mcpClient.ServerInfo.Name);
    }

    [Fact]
    public async Task SseMode_Works_WithSseEndpoint()
    {
        Assert.SkipWhen(Stateless, "SSE endpoint is disabled in stateless mode.");

        Builder.Services.AddMcpServer(options =>
        {
            options.ServerInfo = new()
            {
                Name = "SseTestServer",
                Version = "1.0.0",
            };
        }).WithHttpTransport(ConfigureStateless);
        await using var app = Builder.Build();

        app.MapMcp();

        await app.StartAsync(TestContext.Current.CancellationToken);

        await using var mcpClient = await ConnectAsync(options: new()
        {
            Endpoint = new Uri("http://localhost/sse"),
            TransportMode = HttpTransportMode.Sse
        });

        Assert.Equal("SseTestServer", mcpClient.ServerInfo.Name);
    }

    [Fact]
    public async Task SamplingTool_DoesNotCloseStreamPrematurely()
    {
        Assert.SkipWhen(Stateless, "Sampling is not supported in stateless mode.");

        // Set up a test server with sampling capability
        Builder.Services.AddMcpServer(options =>
        {
            options.ServerInfo = new()
            {
                Name = "SamplingRegressionTestServer",
                Version = "1.0.0",
            };
        }).WithHttpTransport(ConfigureStateless).WithTools<SamplingRegressionTools>();

        await using var app = Builder.Build();
        app.MapMcp();
        await app.StartAsync(TestContext.Current.CancellationToken);

        // Set up client with sampling capability
        var options = new SseClientTransportOptions()
        {
            Endpoint = new Uri("http://localhost/"),
            TransportMode = HttpTransportMode.StreamableHttp,
        };

        var clientOptions = new McpClientOptions();
        clientOptions.Capabilities = new();
        clientOptions.Capabilities.Sampling ??= new();
        clientOptions.Capabilities.Sampling.SamplingHandler = async (_, _, _) =>
        {
            return new CreateMessageResult
            {
                Model = "test-model",
                Role = Role.Assistant,
                Content = new Content
                {
                    Type = "text",
                    Text = "Sampling response from client"
                }
            };
        };

        await using var transport = new SseClientTransport(options, HttpClient, LoggerFactory);
        await using var mcpClient = await McpClientFactory.CreateAsync(transport, clientOptions, LoggerFactory, TestContext.Current.CancellationToken);

        // Call a tool that performs sampling - this should not hang or fail
        var result = await mcpClient.CallToolAsync("sampling-tool", new Dictionary<string, object?>
        {
            ["prompt"] = "Test prompt for sampling"
        }, cancellationToken: TestContext.Current.CancellationToken);

        // Verify we got a successful response
        Assert.NotNull(result);
        Assert.False(result.IsError);
        var textContent = Assert.Single(result.Content);
        Assert.Equal("text", textContent.Type);
        Assert.Contains("Sampling completed successfully", textContent.Text);
    }

    [McpServerToolType]
    public class SamplingRegressionTools
    {
        [McpServerTool(Name = "sampling-tool")]
        public async Task<string> SamplingToolAsync(IMcpServer server, string prompt, CancellationToken cancellationToken)
        {
            // This tool reproduces the exact scenario described in the issue:
            // 1. Client calls tool with request ID 1
            // 2. Tool makes a sampling request which gets ID 1 (auto-incrementing)
            // 3. In the old buggy code, this would close the SSE stream when the sampling request was sent
            // 4. When the client responds and tool tries to send final response, the stream would be closed

            var samplingRequest = new CreateMessageRequestParams
            {
                Messages = [
                    new SamplingMessage
                    {
                        Role = Role.User,
                        Content = new Content
                        {
                            Type = "text",
                            Text = prompt
                        }
                    }
                ],
                MaxTokens = 100
            };

            // This call would trigger the bug in the old implementation
            var samplingResult = await server.SampleAsync(samplingRequest, cancellationToken);

            // If we reach this point, the SSE stream was not closed prematurely
            return $"Sampling completed successfully. Client responded: {samplingResult.Content.Text}";
        }
    }
}
