using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;
using Moq;
using System.Runtime.InteropServices;

namespace ModelContextProtocol.Tests.Server;

public class McpServerFilterTests
{
    private readonly Mock<IMcpServerBuilder> _builder;
    private readonly ServiceCollection _services;

    public McpServerFilterTests()
    {
#if !NET
        Assert.SkipWhen(RuntimeInformation.IsOSPlatform(OSPlatform.Windows), "https://github.com/modelcontextprotocol/csharp-sdk/issues/587");
#endif
        _services = new ServiceCollection();
        _builder = new Mock<IMcpServerBuilder>();
        _builder.SetupGet(b => b.Services).Returns(_services);
    }

    [Fact]
    public void AddListToolsFilter_AddsFilterToHandlers()
    {
        // Arrange
        Func<Func<RequestContext<ListToolsRequestParams>, CancellationToken, ValueTask<ListToolsResult>>, Func<RequestContext<ListToolsRequestParams>, CancellationToken, ValueTask<ListToolsResult>>> filter = next => (context, cancellationToken) =>
        {
            return next(context, cancellationToken);
        };

        // Act
        _builder.Object.AddListToolsFilter(filter);

        // Assert
        var serviceProvider = _services.BuildServiceProvider();
        var options = serviceProvider.GetRequiredService<IOptions<McpServerOptions>>().Value;
        
        // Verify that the filter was added to the options
        Assert.Single(options.Filters.ListToolsFilters);
    }

    [Fact]
    public void AddCallToolFilter_AddsFilterToHandlers()
    {
        // Arrange
        Func<Func<RequestContext<CallToolRequestParams>, CancellationToken, ValueTask<CallToolResult>>, Func<RequestContext<CallToolRequestParams>, CancellationToken, ValueTask<CallToolResult>>> filter = next => (context, cancellationToken) =>
        {
            return next(context, cancellationToken);
        };

        // Act
        _builder.Object.AddCallToolFilter(filter);

        // Assert
        var serviceProvider = _services.BuildServiceProvider();
        var options = serviceProvider.GetRequiredService<IOptions<McpServerOptions>>().Value;
        
        Assert.Single(options.Filters.CallToolFilters);
    }

    [Fact]
    public void AddListPromptsFilter_AddsFilterToHandlers()
    {
        // Arrange
        Func<Func<RequestContext<ListPromptsRequestParams>, CancellationToken, ValueTask<ListPromptsResult>>, Func<RequestContext<ListPromptsRequestParams>, CancellationToken, ValueTask<ListPromptsResult>>> filter = next => (context, cancellationToken) =>
        {
            return next(context, cancellationToken);
        };

        // Act
        _builder.Object.AddListPromptsFilter(filter);

        // Assert
        var serviceProvider = _services.BuildServiceProvider();
        var options = serviceProvider.GetRequiredService<IOptions<McpServerOptions>>().Value;
        
        Assert.Single(options.Filters.ListPromptsFilters);
    }

    [Fact]
    public void AddGetPromptFilter_AddsFilterToHandlers()
    {
        // Arrange
        Func<Func<RequestContext<GetPromptRequestParams>, CancellationToken, ValueTask<GetPromptResult>>, Func<RequestContext<GetPromptRequestParams>, CancellationToken, ValueTask<GetPromptResult>>> filter = next => (context, cancellationToken) =>
        {
            return next(context, cancellationToken);
        };

        // Act
        _builder.Object.AddGetPromptFilter(filter);

        // Assert
        var serviceProvider = _services.BuildServiceProvider();
        var options = serviceProvider.GetRequiredService<IOptions<McpServerOptions>>().Value;
        
        Assert.Single(options.Filters.GetPromptFilters);
    }

    [Fact]
    public void AddListResourcesFilter_AddsFilterToHandlers()
    {
        // Arrange
        Func<Func<RequestContext<ListResourcesRequestParams>, CancellationToken, ValueTask<ListResourcesResult>>, Func<RequestContext<ListResourcesRequestParams>, CancellationToken, ValueTask<ListResourcesResult>>> filter = next => (context, cancellationToken) =>
        {
            return next(context, cancellationToken);
        };

        // Act
        _builder.Object.AddListResourcesFilter(filter);

        // Assert
        var serviceProvider = _services.BuildServiceProvider();
        var options = serviceProvider.GetRequiredService<IOptions<McpServerOptions>>().Value;
        
        Assert.Single(options.Filters.ListResourcesFilters);
    }

    [Fact]
    public void AddReadResourceFilter_AddsFilterToHandlers()
    {
        // Arrange
        Func<Func<RequestContext<ReadResourceRequestParams>, CancellationToken, ValueTask<ReadResourceResult>>, Func<RequestContext<ReadResourceRequestParams>, CancellationToken, ValueTask<ReadResourceResult>>> filter = next => (context, cancellationToken) =>
        {
            return next(context, cancellationToken);
        };

        // Act
        _builder.Object.AddReadResourceFilter(filter);

        // Assert
        var serviceProvider = _services.BuildServiceProvider();
        var options = serviceProvider.GetRequiredService<IOptions<McpServerOptions>>().Value;
        
        Assert.Single(options.Filters.ReadResourceFilters);
    }

    [Fact]
    public void AddCompleteFilter_AddsFilterToHandlers()
    {
        // Arrange
        Func<Func<RequestContext<CompleteRequestParams>, CancellationToken, ValueTask<CompleteResult>>, Func<RequestContext<CompleteRequestParams>, CancellationToken, ValueTask<CompleteResult>>> filter = next => (context, cancellationToken) =>
        {
            return next(context, cancellationToken);
        };

        // Act
        _builder.Object.AddCompleteFilter(filter);

        // Assert
        var serviceProvider = _services.BuildServiceProvider();
        var options = serviceProvider.GetRequiredService<IOptions<McpServerOptions>>().Value;
        
        Assert.Single(options.Filters.CompleteFilters);
    }

    [Fact]
    public void AddSubscribeToResourcesFilter_AddsFilterToHandlers()
    {
        // Arrange
        Func<Func<RequestContext<SubscribeRequestParams>, CancellationToken, ValueTask<EmptyResult>>, Func<RequestContext<SubscribeRequestParams>, CancellationToken, ValueTask<EmptyResult>>> filter = next => (context, cancellationToken) =>
        {
            return next(context, cancellationToken);
        };

        // Act
        _builder.Object.AddSubscribeToResourcesFilter(filter);

        // Assert
        var serviceProvider = _services.BuildServiceProvider();
        var options = serviceProvider.GetRequiredService<IOptions<McpServerOptions>>().Value;
        
        Assert.Single(options.Filters.SubscribeToResourcesFilters);
    }

    [Fact]
    public void AddUnsubscribeFromResourcesFilter_AddsFilterToHandlers()
    {
        // Arrange
        Func<Func<RequestContext<UnsubscribeRequestParams>, CancellationToken, ValueTask<EmptyResult>>, Func<RequestContext<UnsubscribeRequestParams>, CancellationToken, ValueTask<EmptyResult>>> filter = next => (context, cancellationToken) =>
        {
            return next(context, cancellationToken);
        };

        // Act
        _builder.Object.AddUnsubscribeFromResourcesFilter(filter);

        // Assert
        var serviceProvider = _services.BuildServiceProvider();
        var options = serviceProvider.GetRequiredService<IOptions<McpServerOptions>>().Value;
        
        Assert.Single(options.Filters.UnsubscribeFromResourcesFilters);
    }

    [Fact]
    public void AddSetLoggingLevelFilter_AddsFilterToHandlers()
    {
        // Arrange
        Func<Func<RequestContext<SetLevelRequestParams>, CancellationToken, ValueTask<EmptyResult>>, Func<RequestContext<SetLevelRequestParams>, CancellationToken, ValueTask<EmptyResult>>> filter = next => (context, cancellationToken) =>
        {
            return next(context, cancellationToken);
        };

        // Act
        _builder.Object.AddSetLoggingLevelFilter(filter);

        // Assert
        var serviceProvider = _services.BuildServiceProvider();
        var options = serviceProvider.GetRequiredService<IOptions<McpServerOptions>>().Value;
        
        Assert.Single(options.Filters.SetLoggingLevelFilters);
    }

    [Fact]
    public void AddListResourceTemplatesFilter_AddsFilterToHandlers()
    {
        // Arrange
        Func<Func<RequestContext<ListResourceTemplatesRequestParams>, CancellationToken, ValueTask<ListResourceTemplatesResult>>, Func<RequestContext<ListResourceTemplatesRequestParams>, CancellationToken, ValueTask<ListResourceTemplatesResult>>> filter = next => (context, cancellationToken) =>
        {
            return next(context, cancellationToken);
        };

        // Act
        _builder.Object.AddListResourceTemplatesFilter(filter);

        // Assert
        var serviceProvider = _services.BuildServiceProvider();
        var options = serviceProvider.GetRequiredService<IOptions<McpServerOptions>>().Value;
        
        Assert.Single(options.Filters.ListResourceTemplatesFilters);
    }
}
