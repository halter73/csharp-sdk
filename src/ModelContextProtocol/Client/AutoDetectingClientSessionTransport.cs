using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using ModelContextProtocol.Protocol;
using System.Diagnostics;
using System.Threading.Channels;

namespace ModelContextProtocol.Client;

/// <summary>
/// A transport that automatically detects whether to use Streamable HTTP or SSE transport
/// by trying Streamable HTTP first and falling back to SSE if that fails.
/// </summary>
internal sealed class AutoDetectingClientSessionTransport : ITransport
{
    private readonly SseClientTransportOptions _options;
    private readonly HttpClient _httpClient;
    private readonly ILoggerFactory? _loggerFactory;
    private readonly ILogger _logger;
    private readonly string _name;
    
    private StreamableHttpClientSessionTransport? _streamableHttpTransport;
    private SseClientSessionTransport? _sseTransport;
    private readonly Channel<JsonRpcMessage> _messageChannel;

    public AutoDetectingClientSessionTransport(SseClientTransportOptions transportOptions, HttpClient httpClient, ILoggerFactory? loggerFactory, string endpointName)
    {
        Throw.IfNull(transportOptions);
        Throw.IfNull(httpClient);

        _options = transportOptions;
        _httpClient = httpClient;
        _loggerFactory = loggerFactory;
        _logger = (ILogger?)loggerFactory?.CreateLogger<AutoDetectingClientSessionTransport>() ?? NullLogger.Instance;
        _name = endpointName;
        
        // Unbounded channel to prevent blocking on writes
        _messageChannel = Channel.CreateUnbounded<JsonRpcMessage>(new UnboundedChannelOptions
        {
            SingleReader = true,
            SingleWriter = false,
        });
    }

    public ChannelReader<JsonRpcMessage> MessageReader => _messageChannel.Reader;

    /// <inheritdoc/>
    public async Task SendMessageAsync(JsonRpcMessage message, CancellationToken cancellationToken = default)
    {
        if (_streamableHttpTransport == null && _sseTransport == null)
        {
            var rpcRequest = message as JsonRpcRequest;
            
            // The first message must be an initialize request
            Debug.Assert(rpcRequest != null && rpcRequest.Method == RequestMethods.Initialize, 
                "First message must be an initialize request");

            // Try StreamableHttp first
            _streamableHttpTransport = new StreamableHttpClientSessionTransport(_options, _httpClient, _loggerFactory, _name);
            
            try
            {
                var response = await _streamableHttpTransport.SendInitialRequestAsync(message, cancellationToken).ConfigureAwait(false);
                
                // If the status code is not success, fall back to SSE
                if (!response.IsSuccessStatusCode)
                {
                    _logger.LogDebug("Streamable HTTP transport failed for {EndpointName} with status code {StatusCode}, falling back to SSE transport",
                        _name, response.StatusCode);
                    
                    await _streamableHttpTransport.DisposeAsync().ConfigureAwait(false);
                    _streamableHttpTransport = null;
                    
                    await InitializeSseTransportAsync(message, cancellationToken).ConfigureAwait(false);
                    return;
                }
                
                // Process the response
                var responseContent = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
                if (response.Content.Headers.ContentType?.MediaType == "application/json")
                {
                    await ProcessMessageFromStreamableHttpAsync(responseContent, cancellationToken).ConfigureAwait(false);
                }
                else if (response.Content.Headers.ContentType?.MediaType == "text/event-stream")
                {
                    using var responseBodyStream = await response.Content.ReadAsStreamAsync(cancellationToken);
                    await ProcessSseResponseFromStreamableHttpAsync(responseBodyStream, rpcRequest, cancellationToken).ConfigureAwait(false);
                }
                
                // Start forwarding messages
                _ = ForwardMessagesAsync(_streamableHttpTransport.MessageReader, cancellationToken);
            }
            catch (Exception ex)
            {
                _logger.LogDebug(ex, "Streamable HTTP transport failed for {EndpointName}, falling back to SSE transport", _name);
                
                if (_streamableHttpTransport != null)
                {
                    await _streamableHttpTransport.DisposeAsync().ConfigureAwait(false);
                    _streamableHttpTransport = null;
                }
                
                await InitializeSseTransportAsync(message, cancellationToken).ConfigureAwait(false);
            }
        }
        else if (_streamableHttpTransport != null)
        {
            await _streamableHttpTransport.SendMessageAsync(message, cancellationToken).ConfigureAwait(false);
        }
        else if (_sseTransport != null)
        {
            await _sseTransport.SendMessageAsync(message, cancellationToken).ConfigureAwait(false);
        }
    }
    
    private async Task InitializeSseTransportAsync(JsonRpcMessage message, CancellationToken cancellationToken)
    {
        _sseTransport = new SseClientSessionTransport(_options, _httpClient, _loggerFactory, _name);
        await _sseTransport.ConnectAsync(cancellationToken).ConfigureAwait(false);
        await _sseTransport.SendMessageAsync(message, cancellationToken).ConfigureAwait(false);
        
        // Start forwarding messages
        _ = ForwardMessagesAsync(_sseTransport.MessageReader, cancellationToken);
    }
    
    private async Task ProcessMessageFromStreamableHttpAsync(string data, CancellationToken cancellationToken)
    {
        try
        {
            var message = System.Text.Json.JsonSerializer.Deserialize(data, McpJsonUtilities.JsonContext.Default.JsonRpcMessage);
            if (message is null)
            {
                _logger.LogWarning("Failed to parse message from Streamable HTTP response for {EndpointName}", _name);
                return;
            }

            bool wrote = _messageChannel.Writer.TryWrite(message);
            Debug.Assert(wrote, "Failed to write message to channel");
        }
        catch (System.Text.Json.JsonException ex)
        {
            _logger.LogError(ex, "Failed to parse JSON message from Streamable HTTP response for {EndpointName}", _name);
        }
    }
    
    private async Task ProcessSseResponseFromStreamableHttpAsync(Stream responseStream, JsonRpcRequest relatedRpcRequest, CancellationToken cancellationToken)
    {
        await foreach (System.Net.ServerSentEvents.SseItem<string> sseEvent in System.Net.ServerSentEvents.SseParser.Create(responseStream)
                       .EnumerateAsync(cancellationToken).ConfigureAwait(false))
        {
            if (sseEvent.EventType != "message")
            {
                continue;
            }

            await ProcessMessageFromStreamableHttpAsync(sseEvent.Data, cancellationToken).ConfigureAwait(false);
        }
    }
    
    private async Task ForwardMessagesAsync(ChannelReader<JsonRpcMessage> reader, CancellationToken cancellationToken)
    {
        try
        {
            await foreach (var message in reader.ReadAllAsync(cancellationToken).ConfigureAwait(false))
            {
                bool wrote = _messageChannel.Writer.TryWrite(message);
                Debug.Assert(wrote, "Failed to write message to channel");
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            // Expected when cancelling
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error forwarding messages from active transport for {EndpointName}", _name);
        }
    }

    public async ValueTask DisposeAsync()
    {
        try
        {
            if (_streamableHttpTransport != null)
            {
                await _streamableHttpTransport.DisposeAsync().ConfigureAwait(false);
                _streamableHttpTransport = null;
            }
            
            if (_sseTransport != null)
            {
                await _sseTransport.DisposeAsync().ConfigureAwait(false);
                _sseTransport = null;
            }
        }
        finally
        {
            _messageChannel.Writer.Complete();
        }
    }
}