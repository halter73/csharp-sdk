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

        var requestCount = 0;
        mockHttpHandler.RequestHandler = (request) =>
        {
            requestCount++;
            
            // Simulate successful Streamable HTTP response
            return Task.FromResult(new HttpResponseMessage
            {
                StatusCode = HttpStatusCode.OK,
                Content = new StringContent("{\"jsonrpc\":\"2.0\",\"id\":\"test-id\",\"result\":{}}"),
                Headers =
                {
                    { "Content-Type", "application/json" },
                    { "mcp-session-id", "test-session" }
                }
            });
        };

        await using var session = await transport.ConnectAsync(TestContext.Current.CancellationToken);
        
        // The auto-detecting transport should be returned and connected
        Assert.NotNull(session);
        Assert.True(session.IsConnected);
        Assert.IsType<AutoDetectingClientTransport>(session);

        // Send a test message to trigger the transport selection
        await session.SendMessageAsync(new JsonRpcRequest 
        { 
            Method = RequestMethods.Initialize, 
            Id = new RequestId("test-id") 
        }, CancellationToken.None);

        // Verify that we only made one request (Streamable HTTP worked)
        Assert.Equal(1, requestCount);
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
        var isFirstRequest = true;

        mockHttpHandler.RequestHandler = (request) =>
        {
            requestCount++;

            if (isFirstRequest && request.Method == HttpMethod.Post)
            {
                isFirstRequest = false;
                // Simulate Streamable HTTP failure (e.g., 404 Not Found)
                return Task.FromResult(new HttpResponseMessage
                {
                    StatusCode = HttpStatusCode.NotFound,
                    Content = new StringContent("Streamable HTTP not supported")
                });
            }

            // Simulate SSE endpoint response for GET request
            if (request.Method == HttpMethod.Get)
            {
                return Task.FromResult(new HttpResponseMessage
                {
                    StatusCode = HttpStatusCode.OK,
                    Content = new StringContent("event: endpoint\r\ndata: /sse-endpoint\r\n\r\n"),
                    Headers = { { "Content-Type", "text/event-stream" } }
                });
            }

            // Simulate successful SSE POST response
            return Task.FromResult(new HttpResponseMessage
            {
                StatusCode = HttpStatusCode.OK,
                Content = new StringContent("accepted")
            });
        };

        await using var session = await transport.ConnectAsync(TestContext.Current.CancellationToken);
        
        // The auto-detecting transport should be returned
        Assert.NotNull(session);
        Assert.IsType<AutoDetectingClientTransport>(session);

        // Send a test message to trigger the transport selection and fallback
        await session.SendMessageAsync(new JsonRpcRequest 
        { 
            Method = RequestMethods.Initialize, 
            Id = new RequestId("test-id") 
        }, CancellationToken.None);

        // Verify that we made multiple requests (Streamable HTTP failed, SSE succeeded)
        Assert.True(requestCount >= 2, $"Expected at least 2 requests, but got {requestCount}");
    }

    [Fact]
    public async Task AutoDetect_Should_Be_Default_When_UseStreamableHttp_Is_False()
    {
        var options = new SseClientTransportOptions
        {
            Endpoint = new Uri("http://localhost:8080"),
            UseStreamableHttp = false, // This should map to AutoDetect
            ConnectionTimeout = TimeSpan.FromSeconds(2),
            Name = "Test Server"
        };

        using var mockHttpHandler = new MockHttpHandler();
        using var httpClient = new HttpClient(mockHttpHandler);
        await using var transport = new SseClientTransport(options, httpClient, LoggerFactory);

        mockHttpHandler.RequestHandler = (request) =>
        {
            // Simulate successful Streamable HTTP response
            return Task.FromResult(new HttpResponseMessage
            {
                StatusCode = HttpStatusCode.OK,
                Content = new StringContent("{\"jsonrpc\":\"2.0\",\"id\":\"test-id\",\"result\":{}}"),
                Headers = { { "Content-Type", "application/json" } }
            });
        };

        await using var session = await transport.ConnectAsync(TestContext.Current.CancellationToken);
        
        // Should return AutoDetectingClientTransport when UseStreamableHttp is false
        Assert.IsType<AutoDetectingClientTransport>(session);
    }

    [Fact]
    public async Task StreamableHttp_Mode_Should_Return_StreamableHttp_Transport()
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
}