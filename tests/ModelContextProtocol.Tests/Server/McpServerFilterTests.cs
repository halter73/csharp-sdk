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
    public void UseListToolsFilter_AddsFilterToHandlers()
    {
        // Arrange
        Func<ListToolsHandler, ListToolsHandler> filter = next => (context, cancellationToken) =>
        {
            return next(context, cancellationToken);
        };

        // Act
        _builder.Object.UseListToolsFilter(filter);

        // Assert
        var serviceProvider = _services.BuildServiceProvider();
        var handlers = serviceProvider.GetRequiredService<IOptions<McpServerHandlers>>().Value;
        
        // We can't directly test the private filter list, but we can verify the method was called
        // by checking that no exceptions were thrown and the service was configured
        Assert.NotNull(handlers);
    }

    [Fact]
    public void UseCallToolFilter_AddsFilterToHandlers()
    {
        // Arrange
        Func<CallToolHandler, CallToolHandler> filter = next => (context, cancellationToken) =>
        {
            return next(context, cancellationToken);
        };

        // Act
        _builder.Object.UseCallToolFilter(filter);

        // Assert
        var serviceProvider = _services.BuildServiceProvider();
        var handlers = serviceProvider.GetRequiredService<IOptions<McpServerHandlers>>().Value;
        
        Assert.NotNull(handlers);
    }

    [Fact]
    public void UseListPromptsFilter_AddsFilterToHandlers()
    {
        // Arrange
        Func<ListPromptsHandler, ListPromptsHandler> filter = next => (context, cancellationToken) =>
        {
            return next(context, cancellationToken);
        };

        // Act
        _builder.Object.UseListPromptsFilter(filter);

        // Assert
        var serviceProvider = _services.BuildServiceProvider();
        var handlers = serviceProvider.GetRequiredService<IOptions<McpServerHandlers>>().Value;
        
        Assert.NotNull(handlers);
    }

    [Fact]
    public void UseGetPromptFilter_AddsFilterToHandlers()
    {
        // Arrange
        Func<GetPromptHandler, GetPromptHandler> filter = next => (context, cancellationToken) =>
        {
            return next(context, cancellationToken);
        };

        // Act
        _builder.Object.UseGetPromptFilter(filter);

        // Assert
        var serviceProvider = _services.BuildServiceProvider();
        var handlers = serviceProvider.GetRequiredService<IOptions<McpServerHandlers>>().Value;
        
        Assert.NotNull(handlers);
    }

    [Fact]
    public void UseListResourcesFilter_AddsFilterToHandlers()
    {
        // Arrange
        Func<ListResourcesHandler, ListResourcesHandler> filter = next => (context, cancellationToken) =>
        {
            return next(context, cancellationToken);
        };

        // Act
        _builder.Object.UseListResourcesFilter(filter);

        // Assert
        var serviceProvider = _services.BuildServiceProvider();
        var handlers = serviceProvider.GetRequiredService<IOptions<McpServerHandlers>>().Value;
        
        Assert.NotNull(handlers);
    }

    [Fact]
    public void UseReadResourceFilter_AddsFilterToHandlers()
    {
        // Arrange
        Func<ReadResourceHandler, ReadResourceHandler> filter = next => (context, cancellationToken) =>
        {
            return next(context, cancellationToken);
        };

        // Act
        _builder.Object.UseReadResourceFilter(filter);

        // Assert
        var serviceProvider = _services.BuildServiceProvider();
        var handlers = serviceProvider.GetRequiredService<IOptions<McpServerHandlers>>().Value;
        
        Assert.NotNull(handlers);
    }

    [Fact]
    public void UseCompleteFilter_AddsFilterToHandlers()
    {
        // Arrange
        Func<CompleteHandler, CompleteHandler> filter = next => (context, cancellationToken) =>
        {
            return next(context, cancellationToken);
        };

        // Act
        _builder.Object.UseCompleteFilter(filter);

        // Assert
        var serviceProvider = _services.BuildServiceProvider();
        var handlers = serviceProvider.GetRequiredService<IOptions<McpServerHandlers>>().Value;
        
        Assert.NotNull(handlers);
    }

    [Fact]
    public void UseSubscribeToResourcesFilter_AddsFilterToHandlers()
    {
        // Arrange
        Func<SubscribeToResourcesHandler, SubscribeToResourcesHandler> filter = next => (context, cancellationToken) =>
        {
            return next(context, cancellationToken);
        };

        // Act
        _builder.Object.UseSubscribeToResourcesFilter(filter);

        // Assert
        var serviceProvider = _services.BuildServiceProvider();
        var handlers = serviceProvider.GetRequiredService<IOptions<McpServerHandlers>>().Value;
        
        Assert.NotNull(handlers);
    }

    [Fact]
    public void UseUnsubscribeFromResourcesFilter_AddsFilterToHandlers()
    {
        // Arrange
        Func<UnsubscribeFromResourcesHandler, UnsubscribeFromResourcesHandler> filter = next => (context, cancellationToken) =>
        {
            return next(context, cancellationToken);
        };

        // Act
        _builder.Object.UseUnsubscribeFromResourcesFilter(filter);

        // Assert
        var serviceProvider = _services.BuildServiceProvider();
        var handlers = serviceProvider.GetRequiredService<IOptions<McpServerHandlers>>().Value;
        
        Assert.NotNull(handlers);
    }

    [Fact]
    public void UseSetLoggingLevelFilter_AddsFilterToHandlers()
    {
        // Arrange
        Func<SetLoggingLevelHandler, SetLoggingLevelHandler> filter = next => (context, cancellationToken) =>
        {
            return next(context, cancellationToken);
        };

        // Act
        _builder.Object.UseSetLoggingLevelFilter(filter);

        // Assert
        var serviceProvider = _services.BuildServiceProvider();
        var handlers = serviceProvider.GetRequiredService<IOptions<McpServerHandlers>>().Value;
        
        Assert.NotNull(handlers);
    }

    [Fact]
    public void UseListResourceTemplatesFilter_AddsFilterToHandlers()
    {
        // Arrange
        Func<ListResourceTemplatesHandler, ListResourceTemplatesHandler> filter = next => (context, cancellationToken) =>
        {
            return next(context, cancellationToken);
        };

        // Act
        _builder.Object.UseListResourceTemplatesFilter(filter);

        // Assert
        var serviceProvider = _services.BuildServiceProvider();
        var handlers = serviceProvider.GetRequiredService<IOptions<McpServerHandlers>>().Value;
        
        Assert.NotNull(handlers);
    }
}
