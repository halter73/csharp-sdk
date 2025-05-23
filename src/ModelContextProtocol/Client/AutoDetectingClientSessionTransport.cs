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
internal sealed partial class AutoDetectingClientSessionTransport : ITransport
{
    private readonly SseClientTransportOptions _options;
    private readonly HttpClient _httpClient;
    private readonly ILoggerFactory? _loggerFactory;
    private readonly ILogger _logger;
    private readonly string _name;
    private readonly DelegatingChannelReader<JsonRpcMessage> _delegatingChannelReader;
    
    private StreamableHttpClientSessionTransport? _streamableHttpTransport;
    private SseClientSessionTransport? _sseTransport;

    public AutoDetectingClientSessionTransport(SseClientTransportOptions transportOptions, HttpClient httpClient, ILoggerFactory? loggerFactory, string endpointName)
    {
        Throw.IfNull(transportOptions);
        Throw.IfNull(httpClient);

        _options = transportOptions;
        _httpClient = httpClient;
        _loggerFactory = loggerFactory;
        _logger = (ILogger?)loggerFactory?.CreateLogger<AutoDetectingClientSessionTransport>() ?? NullLogger.Instance;
        _name = endpointName;
        _delegatingChannelReader = new DelegatingChannelReader<JsonRpcMessage>(this);
    }

    /// <summary>
    /// Returns the active transport (either StreamableHttp or SSE)
    /// </summary>
    internal ITransport? ActiveTransport => _streamableHttpTransport != null ? (ITransport)_streamableHttpTransport : _sseTransport;

    public ChannelReader<JsonRpcMessage> MessageReader => _delegatingChannelReader;

    /// <inheritdoc/>
    public async Task SendMessageAsync(JsonRpcMessage message, CancellationToken cancellationToken = default)
    {
        if (_streamableHttpTransport == null && _sseTransport == null)
        {
            var rpcRequest = message as JsonRpcRequest;

            // Try StreamableHttp first
            _streamableHttpTransport = new StreamableHttpClientSessionTransport(_options, _httpClient, _loggerFactory, _name);
            
            try
            {
                LogAttemptingStreamableHttp(_name);
                var response = await _streamableHttpTransport.SendInitialRequestAsync(message, cancellationToken).ConfigureAwait(false);
                
                // If the status code is not success, fall back to SSE
                if (!response.IsSuccessStatusCode)
                {
                    LogStreamableHttpFailed(_name, response.StatusCode);
                    
                    try
                    {
                        await _streamableHttpTransport.DisposeAsync().ConfigureAwait(false);
                    }
                    finally
                    {
                        _streamableHttpTransport = null;
                        await InitializeSseTransportAsync(message, cancellationToken).ConfigureAwait(false);
                    }
                    return;
                }
                
                // Process the streamable HTTP response using the transport
                await _streamableHttpTransport.SendMessageAsync(message, cancellationToken).ConfigureAwait(false);
                
                // Signal that we have established a connection
                LogUsingStreamableHttp(_name);
                _delegatingChannelReader.SetConnected();
            }
            catch (Exception ex)
            {
                LogStreamableHttpException(_name, ex);
                
                try
                {
                    if (_streamableHttpTransport != null)
                    {
                        await _streamableHttpTransport.DisposeAsync().ConfigureAwait(false);
                        _streamableHttpTransport = null;
                    }
                }
                catch (Exception disposeEx)
                {
                    LogDisposeFailed(_name, disposeEx);
                }
                
                // Propagate the original exception
                throw;
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
        Exception? capturedEx = null;
        try
        {
            LogAttemptingSSE(_name);
            _sseTransport = new SseClientSessionTransport(_options, _httpClient, _loggerFactory, _name);
            await _sseTransport.ConnectAsync(cancellationToken).ConfigureAwait(false);
            await _sseTransport.SendMessageAsync(message, cancellationToken).ConfigureAwait(false);
            
            // Signal that we have established a connection
            LogUsingSSE(_name);
            _delegatingChannelReader.SetConnected();
        }
        catch (Exception ex)
        {
            LogSSEConnectionFailed(_name, ex);
            capturedEx = ex;
            
            try
            {
                if (_sseTransport != null)
                {
                    await _sseTransport.DisposeAsync().ConfigureAwait(false);
                }
            }
            finally
            {
                // Set the error so the channel reader will propagate it
                _delegatingChannelReader.SetError(ex);
            }
            
            throw;
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
        catch (Exception ex)
        {
            LogDisposeFailed(_name, ex);
        }
    }
    
    [LoggerMessage(Level = LogLevel.Debug, Message = "{EndpointName}: Attempting to connect using Streamable HTTP transport")]
    private partial void LogAttemptingStreamableHttp(string endpointName);
    
    [LoggerMessage(Level = LogLevel.Debug, Message = "{EndpointName}: Streamable HTTP transport failed with status code {StatusCode}, falling back to SSE transport")]
    private partial void LogStreamableHttpFailed(string endpointName, System.Net.HttpStatusCode statusCode);
    
    [LoggerMessage(Level = LogLevel.Debug, Message = "{EndpointName}: Streamable HTTP transport failed with exception, falling back to SSE transport")]
    private partial void LogStreamableHttpException(string endpointName, Exception exception);
    
    [LoggerMessage(Level = LogLevel.Debug, Message = "{EndpointName}: Using Streamable HTTP transport")]
    private partial void LogUsingStreamableHttp(string endpointName);
    
    [LoggerMessage(Level = LogLevel.Debug, Message = "{EndpointName}: Attempting to connect using SSE transport")]
    private partial void LogAttemptingSSE(string endpointName);
    
    [LoggerMessage(Level = LogLevel.Debug, Message = "{EndpointName}: Using SSE transport")]
    private partial void LogUsingSSE(string endpointName);
    
    [LoggerMessage(Level = LogLevel.Error, Message = "{EndpointName}: SSE transport connection failed")]
    private partial void LogSSEConnectionFailed(string endpointName, Exception exception);
    
    [LoggerMessage(Level = LogLevel.Warning, Message = "{EndpointName}: Error disposing transport")]
    private partial void LogDisposeFailed(string endpointName, Exception exception);
}