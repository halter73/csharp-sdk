using Microsoft.Extensions.Logging;
using ModelContextProtocol.Client;
using ModelContextProtocol.Protocol;
using System.Net;
using System.Text;
using System.Text.Json;
using Xunit;

namespace ModelContextProtocol.Tests.Client;

/// <summary>
/// Tests to verify that the StreamableHttpClientSessionTransport sends the MCP-Protocol-Version header
/// as required by the MCP specification.
/// </summary>
public class StreamableHttpProtocolVersionHeaderTests
{
    /// <summary>
    /// A mock HTTP handler that captures the last request for inspection.
    /// </summary>
    private class CapturingMockHttpHandler : HttpMessageHandler
    {
        public HttpRequestMessage? LastRequest { get; private set; }
        private HttpResponseMessage? _response;

        public void SetResponse(HttpStatusCode statusCode, string contentType, string content)
        {
            _response = new HttpResponseMessage(statusCode)
            {
                Content = new StringContent(content, Encoding.UTF8, contentType)
            };
        }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            LastRequest = request;
            return Task.FromResult(_response ?? throw new InvalidOperationException("No response configured"));
        }
    }

    [Fact]
    public async Task SendHttpRequestAsync_AfterInitialization_ShouldIncludeMcpProtocolVersionHeader()
    {
        // Arrange
        const string expectedProtocolVersion = "2024-11-05";
        var mockHandler = new CapturingMockHttpHandler();
        var httpClient = new HttpClient(mockHandler);
        var options = new SseClientTransportOptions
        {
            Endpoint = new Uri("http://test.example/mcp")
        };

        var transport = new StreamableHttpClientSessionTransport(
            "test-endpoint",
            options,
            httpClient,
            null,
            null);

        // Set the negotiated protocol version (simulating successful initialization)
        transport.SetNegotiatedProtocolVersion(expectedProtocolVersion);

        var testMessage = new JsonRpcRequest
        {
            Id = new RequestId(1),
            Method = "test/method",
            Params = JsonSerializer.SerializeToNode(new { test = "value" })
        };

        // Configure mock to return success response
        mockHandler.SetResponse(HttpStatusCode.OK, "application/json", 
            JsonSerializer.Serialize(new JsonRpcResponse 
            { 
                Id = testMessage.Id, 
                Result = JsonSerializer.SerializeToNode(new { success = true }) 
            }));

        // Act
        using var response = await transport.SendHttpRequestAsync(testMessage, CancellationToken.None);

        // Assert
        Assert.NotNull(mockHandler.LastRequest);
        Assert.True(mockHandler.LastRequest.Headers.Contains("MCP-Protocol-Version"), 
            "Expected MCP-Protocol-Version header to be present");
        
        var headerValues = mockHandler.LastRequest.Headers.GetValues("MCP-Protocol-Version").ToArray();
        Assert.Single(headerValues);
        Assert.Equal(expectedProtocolVersion, headerValues[0]);
    }

    [Fact]
    public async Task SendHttpRequestAsync_BeforeInitialization_ShouldNotIncludeMcpProtocolVersionHeader()
    {
        // Arrange
        var mockHandler = new CapturingMockHttpHandler();
        var httpClient = new HttpClient(mockHandler);
        var options = new SseClientTransportOptions
        {
            Endpoint = new Uri("http://test.example/mcp")
        };

        var transport = new StreamableHttpClientSessionTransport(
            "test-endpoint",
            options,
            httpClient,
            null,
            null);

        // Note: NOT setting the negotiated protocol version (simulating before initialization)

        var testMessage = new JsonRpcRequest
        {
            Id = new RequestId(1),
            Method = "initialize",
            Params = JsonSerializer.SerializeToNode(new InitializeRequestParams
            {
                ProtocolVersion = "2024-11-05",
                Capabilities = new ClientCapabilities(),
                ClientInfo = new Implementation { Name = "test-client", Version = "1.0.0" }
            })
        };

        // Configure mock to return success response
        mockHandler.SetResponse(HttpStatusCode.OK, "application/json", 
            JsonSerializer.Serialize(new JsonRpcResponse 
            { 
                Id = testMessage.Id, 
                Result = JsonSerializer.SerializeToNode(new InitializeResult
                {
                    ProtocolVersion = "2024-11-05",
                    Capabilities = new ServerCapabilities(),
                    ServerInfo = new Implementation { Name = "test-server", Version = "1.0.0" }
                })
            }));

        // Act
        using var response = await transport.SendHttpRequestAsync(testMessage, CancellationToken.None);

        // Assert
        Assert.NotNull(mockHandler.LastRequest);
        Assert.False(mockHandler.LastRequest.Headers.Contains("MCP-Protocol-Version"), 
            "Expected MCP-Protocol-Version header to NOT be present before initialization");
    }
}