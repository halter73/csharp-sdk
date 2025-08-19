#if false
using Microsoft.Extensions.Logging.Abstractions;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;
using ModelContextProtocol.Tests.Utils;

namespace ModelContextProtocol.Tests.Server;

public class McpServerMatchedPrimitiveTests
{
    [Fact]
    public async Task CallToolHandler_SetsMatchedPrimitive()
    {
        // Arrange
        var mockTool = new MockTool();
        var tools = new McpServerPrimitiveCollection<McpServerTool>();
        tools.Add(mockTool);

        IMcpServerPrimitive? receivedMatchedPrimitive = null;

        var capabilities = new ServerCapabilities
        {
            Tools = new ToolsCapability
            {
                ToolCollection = tools,
            }
        };

        var options = new McpServerOptions
        {
            ProtocolVersion = "2024-11-05",
            InitializationTimeout = TimeSpan.FromSeconds(30),
            Capabilities = capabilities
        };

        // Add a filter that captures the MatchedPrimitive
        options.Filters.CallToolFilters.Add(next => (context, cancellationToken) =>
        {
            receivedMatchedPrimitive = context.MatchedPrimitive;
            return next(context, cancellationToken);
        });

        await using var transport = new TestServerTransport();
        await using var server = McpServerFactory.Create(transport, options, NullLoggerFactory.Instance);

        // Act - Call the handler through the server's internal capabilities
        var capabilities_internal = ((McpServer)server).ServerCapabilities;
        var result = await capabilities_internal.Tools.CallToolHandler!(
            new RequestContext<CallToolRequestParams>((IMcpServer)server)
            {
                Params = new CallToolRequestParams { Name = "test-tool" }
            },
            CancellationToken.None);

        // Assert
        Assert.NotNull(receivedMatchedPrimitive);
        Assert.Same(mockTool, receivedMatchedPrimitive);
        Assert.Equal("test-result", ((TextContentBlock)result.Content[0]).Text);
    }

    [Fact]
    public async Task GetPromptHandler_SetsMatchedPrimitive()
    {
        // Arrange
        var mockPrompt = new MockPrompt();
        var prompts = new McpServerPrimitiveCollection<McpServerPrompt>();
        prompts.Add(mockPrompt);

        IMcpServerPrimitive? receivedMatchedPrimitive = null;

        var capabilities = new ServerCapabilities
        {
            Prompts = new PromptsCapability
            {
                PromptCollection = prompts,
            }
        };

        var options = new McpServerOptions
        {
            ProtocolVersion = "2024-11-05",
            InitializationTimeout = TimeSpan.FromSeconds(30),
            Capabilities = capabilities
        };

        // Add a filter that captures the MatchedPrimitive
        options.Filters.GetPromptFilters.Add(next => (context, cancellationToken) =>
        {
            receivedMatchedPrimitive = context.MatchedPrimitive;
            return next(context, cancellationToken);
        });

        await using var transport = new TestServerTransport();
        await using var server = McpServerFactory.Create(transport, options, NullLoggerFactory.Instance);

        // Act - Call the handler through the server's internal capabilities
        var capabilities_internal = ((McpServer)server).ServerCapabilities;
        var result = await capabilities_internal.Prompts.GetPromptHandler!(
            new RequestContext<GetPromptRequestParams>((IMcpServer)server)
            {
                Params = new GetPromptRequestParams { Name = "test-prompt" }
            },
            CancellationToken.None);

        // Assert
        Assert.NotNull(receivedMatchedPrimitive);
        Assert.Same(mockPrompt, receivedMatchedPrimitive);
        Assert.Equal("test-prompt-result", result.Messages[0].Content.Text);
    }

    [Fact]
    public async Task ReadResourceHandler_SetsMatchedPrimitive()
    {
        // Arrange
        var mockResource = new MockResource();
        var resources = new McpServerResourceCollection();
        resources.Add(mockResource);

        IMcpServerPrimitive? receivedMatchedPrimitive = null;

        var capabilities = new ServerCapabilities
        {
            Resources = new ResourcesCapability
            {
                ResourceCollection = resources,
            }
        };

        var options = new McpServerOptions
        {
            ProtocolVersion = "2024-11-05",
            InitializationTimeout = TimeSpan.FromSeconds(30),
            Capabilities = capabilities
        };

        // Add a filter that captures the MatchedPrimitive
        options.Filters.ReadResourceFilters.Add(next => async (context, cancellationToken) =>
        {
            receivedMatchedPrimitive = context.MatchedPrimitive;
            return await next(context, cancellationToken);
        });

        await using var transport = new TestServerTransport();
        await using var server = McpServerFactory.Create(transport, options, NullLoggerFactory.Instance);

        // Act - Call the handler through the server's internal capabilities
        var capabilities_internal = ((McpServer)server).ServerCapabilities;
        var result = await capabilities_internal.Resources.ReadResourceHandler!(
            new RequestContext<ReadResourceRequestParams>((IMcpServer)server)
            {
                Params = new ReadResourceRequestParams { Uri = "test://resource" }
            },
            CancellationToken.None);

        // Assert
        Assert.NotNull(receivedMatchedPrimitive);
        Assert.Same(mockResource, receivedMatchedPrimitive);
        Assert.Equal("test-resource-content", ((TextResourceContents)result.Contents[0]).Text);
    }

    private class MockTool : McpServerTool
    {
        public override Tool ProtocolTool => new() { Name = "test-tool", Description = "Test tool" };
        public override IList<object> Metadata => [];

        public override ValueTask<CallToolResult> InvokeAsync(RequestContext<CallToolRequestParams> request, CancellationToken cancellationToken)
        {
            return new ValueTask<CallToolResult>(new CallToolResult
            {
                Content = [new TextContentBlock { Text = "test-result" }]
            });
        }
    }

    private class MockPrompt : McpServerPrompt
    {
        public override Prompt ProtocolPrompt => new() { Name = "test-prompt", Description = "Test prompt" };
        public override IList<object> Metadata => [];

        public override ValueTask<GetPromptResult> GetAsync(RequestContext<GetPromptRequestParams> request, CancellationToken cancellationToken)
        {
            return new ValueTask<GetPromptResult>(new GetPromptResult
            {
                Messages = [new PromptMessage
                {
                    Role = Role.User,
                    Content = new TextContentBlock { Text = "test-prompt-result" }
                }]
            });
        }
    }

    private class MockResource : McpServerResource
    {
        public override ResourceTemplate ProtocolResourceTemplate => new() { UriTemplate = "test://resource", Name = "Test Resource" };
        public override IList<object> Metadata => [];

        public override ValueTask<ReadResourceResult?> ReadAsync(RequestContext<ReadResourceRequestParams> request, CancellationToken cancellationToken)
        {
            return new ValueTask<ReadResourceResult?>(new ReadResourceResult
            {
                Contents = [new TextResourceContents { Uri = "test://resource", Text = "test-resource-content" }]
            });
        }
    }
}
#endif