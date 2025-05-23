using ModelContextProtocol.Client;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Tests.Utils;
using System.Net;

namespace ModelContextProtocol.Tests.Transport;

public class SseClientTransportAutoDetectTests : LoggedTest
{
    public SseClientTransportAutoDetectTests(ITestOutputHelper testOutputHelper)
        : base(testOutputHelper)
    {
    }

    [Fact]
    public async Task AutoDetect_Should_Use_StreamableHttp_When_Server_Supports_It()
    {
        var options = new SseClientTransportOptions
        {
            Endpoint = new Uri("http://localhost:8080"),
            TransportMode = SseTransportMode.AutoDetect,
            ConnectionTimeout = TimeSpan.FromSeconds(2),
            Name = "Test Server"
        };

        using var mockHttpHandler = new MockHttpHandler();
        using var httpClient = new HttpClient(mockHttpHandler);
        await using var transport = new SseClientTransport(options, httpClient, LoggerFactory);

        // Simulate successful Streamable HTTP response for initialize
        mockHttpHandler.RequestHandler = (request) =>
        {
            if (request.Method == HttpMethod.Post)
            {
                return Task.FromResult(new HttpResponseMessage
                {
                    StatusCode = HttpStatusCode.OK,
                    Content = new StringContent("{\"jsonrpc\":\"2.0\",\"id\":\"init-id\",\"result\":{\"protocolVersion\":\"2024-11-05\",\"capabilities\":{\"tools\":{}}}}"),
                    Headers =
                    {
                        { "Content-Type", "application/json" },
                        { "mcp-session-id", "test-session" }
                    }
                });
            }

            // Shouldn't reach here for successful Streamable HTTP
            throw new InvalidOperationException("Unexpected request");
        };

        await using var session = await transport.ConnectAsync(TestContext.Current.CancellationToken);
        
        // The auto-detecting transport should be returned
        Assert.NotNull(session);
        Assert.True(session.IsConnected);
        Assert.IsType<AutoDetectingClientTransport>(session);
    }

    [Fact] 
    public async Task AutoDetect_Should_Fallback_To_Sse_When_StreamableHttp_Fails()
    {
        var options = new SseClientTransportOptions
        {
            Endpoint = new Uri("http://localhost:8080"),
            TransportMode = SseTransportMode.AutoDetect,
            ConnectionTimeout = TimeSpan.FromSeconds(2),
            Name = "Test Server"
        };

        using var mockHttpHandler = new MockHttpHandler();
        using var httpClient = new HttpClient(mockHttpHandler);
        await using var transport = new SseClientTransport(options, httpClient, LoggerFactory);

        var requestCount = 0;

        mockHttpHandler.RequestHandler = (request) =>
        {
            requestCount++;

            if (request.Method == HttpMethod.Post && requestCount == 1)
            {
                // First POST (Streamable HTTP) fails
                return Task.FromResult(new HttpResponseMessage
                {
                    StatusCode = HttpStatusCode.NotFound,
                    Content = new StringContent("Streamable HTTP not supported")
                });
            }

            if (request.Method == HttpMethod.Get)
            {
                // SSE connection request
                return Task.FromResult(new HttpResponseMessage
                {
                    StatusCode = HttpStatusCode.OK,
                    Content = new StringContent("event: endpoint\r\ndata: /sse-endpoint\r\n\r\n"),
                    Headers = { { "Content-Type", "text/event-stream" } }
                });
            }

            if (request.Method == HttpMethod.Post && requestCount > 1)
            {
                // Subsequent POST to SSE endpoint succeeds
                return Task.FromResult(new HttpResponseMessage
                {
                    StatusCode = HttpStatusCode.OK,
                    Content = new StringContent("accepted")
                });
            }

            throw new InvalidOperationException($"Unexpected request: {request.Method}, count: {requestCount}");
        };

        await using var session = await transport.ConnectAsync(TestContext.Current.CancellationToken);
        
        // The auto-detecting transport should be returned
        Assert.NotNull(session);
        Assert.True(session.IsConnected);
        Assert.IsType<AutoDetectingClientTransport>(session);
    }

    [Fact]
    public async Task TransportMode_AutoDetect_Should_Use_AutoDetectingTransport()
    {
        var options = new SseClientTransportOptions
        {
            Endpoint = new Uri("http://localhost:8080"),
            TransportMode = SseTransportMode.AutoDetect,
            ConnectionTimeout = TimeSpan.FromSeconds(2),
            Name = "Test Server"
        };

        using var mockHttpHandler = new MockHttpHandler();
        using var httpClient = new HttpClient(mockHttpHandler);
        await using var transport = new SseClientTransport(options, httpClient, LoggerFactory);

        // Configure for successful Streamable HTTP response
        mockHttpHandler.RequestHandler = (request) =>
        {
            return Task.FromResult(new HttpResponseMessage
            {
                StatusCode = HttpStatusCode.OK,
                Content = new StringContent("{\"jsonrpc\":\"2.0\",\"id\":\"test-id\",\"result\":{}}"),
                Headers = { { "Content-Type", "application/json" } }
            });
        };

        await using var session = await transport.ConnectAsync(TestContext.Current.CancellationToken);
        
        // Should return AutoDetectingClientTransport when using AutoDetect mode
        Assert.IsType<AutoDetectingClientTransport>(session);
    }

    [Fact]
    public async Task TransportMode_StreamableHttp_Should_Return_StreamableHttp_Transport()
    {
        var options = new SseClientTransportOptions
        {
            Endpoint = new Uri("http://localhost:8080"),
            TransportMode = SseTransportMode.StreamableHttp,
            ConnectionTimeout = TimeSpan.FromSeconds(2),
            Name = "Test Server"
        };

        using var mockHttpHandler = new MockHttpHandler();
        using var httpClient = new HttpClient(mockHttpHandler);
        await using var transport = new SseClientTransport(options, httpClient, LoggerFactory);

        await using var session = await transport.ConnectAsync(TestContext.Current.CancellationToken);
        
        // Should return StreamableHttpClientSessionTransport directly
        Assert.IsType<StreamableHttpClientSessionTransport>(session);
    }

    [Fact]
    public async Task Sse_Mode_Should_Return_Sse_Transport()
    {
        var options = new SseClientTransportOptions
        {
            Endpoint = new Uri("http://localhost:8080"),
            TransportMode = SseTransportMode.Sse,
            ConnectionTimeout = TimeSpan.FromSeconds(2),
            Name = "Test Server"
        };

        using var mockHttpHandler = new MockHttpHandler();
        using var httpClient = new HttpClient(mockHttpHandler);
        await using var transport = new SseClientTransport(options, httpClient, LoggerFactory);

        mockHttpHandler.RequestHandler = (request) =>
        {
            // Simulate SSE endpoint response
            return Task.FromResult(new HttpResponseMessage
            {
                StatusCode = HttpStatusCode.OK,
                Content = new StringContent("event: endpoint\r\ndata: /sse-endpoint\r\n\r\n"),
                Headers = { { "Content-Type", "text/event-stream" } }
            });
        };

        await using var session = await transport.ConnectAsync(TestContext.Current.CancellationToken);
        
        // Should return SseClientSessionTransport directly
        Assert.IsType<SseClientSessionTransport>(session);
    }

    [Fact]
    public void GetEffectiveTransportMode_Should_Return_Configured_TransportMode()
    {
        var options1 = new SseClientTransportOptions
        {
            Endpoint = new Uri("http://localhost:8080"),
            TransportMode = SseTransportMode.StreamableHttp
        };
        Assert.Equal(SseTransportMode.StreamableHttp, options1.GetEffectiveTransportMode());

        var options2 = new SseClientTransportOptions
        {
            Endpoint = new Uri("http://localhost:8080"),
            TransportMode = SseTransportMode.Sse
        };
        Assert.Equal(SseTransportMode.Sse, options2.GetEffectiveTransportMode());

        var options3 = new SseClientTransportOptions
        {
            Endpoint = new Uri("http://localhost:8080"),
            TransportMode = SseTransportMode.AutoDetect
        };
        Assert.Equal(SseTransportMode.AutoDetect, options3.GetEffectiveTransportMode());
    }

    [Fact]
    public void GetEffectiveTransportMode_Should_Default_To_AutoDetect()
    {
        var options = new SseClientTransportOptions
        {
            Endpoint = new Uri("http://localhost:8080")
        };
        Assert.Equal(SseTransportMode.AutoDetect, options.GetEffectiveTransportMode());
    }
}