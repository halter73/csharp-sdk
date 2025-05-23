using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using ModelContextProtocol.Protocol;

namespace ModelContextProtocol.Client;

/// <summary>
/// A transport that automatically detects whether to use Streamable HTTP or SSE transport
/// by trying Streamable HTTP first and falling back to SSE if that fails.
/// </summary>
internal sealed class AutoDetectingClientTransport : TransportBase
{
    private readonly SseClientTransportOptions _options;
    private readonly HttpClient _httpClient;
    private readonly ILoggerFactory? _loggerFactory;
    private readonly ILogger _logger;
    
    private ITransport? _activeTransport;
    private bool _hasAttemptedStreamableHttp;
    private Task? _messageForwardingTask;
    private CancellationTokenSource? _messageForwardingCts;

    public AutoDetectingClientTransport(SseClientTransportOptions transportOptions, HttpClient httpClient, ILoggerFactory? loggerFactory, string endpointName)
        : base(endpointName, loggerFactory)
    {
        Throw.IfNull(transportOptions);
        Throw.IfNull(httpClient);

        _options = transportOptions;
        _httpClient = httpClient;
        _loggerFactory = loggerFactory;
        _logger = (ILogger?)loggerFactory?.CreateLogger<AutoDetectingClientTransport>() ?? NullLogger.Instance;
    }

    /// <inheritdoc/>
    public override async Task SendMessageAsync(JsonRpcMessage message, CancellationToken cancellationToken = default)
    {
        if (_activeTransport == null)
        {
            await EnsureConnectedAsync(cancellationToken).ConfigureAwait(false);
        }

        try
        {
            await _activeTransport!.SendMessageAsync(message, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex) when (!_hasAttemptedStreamableHttp && _activeTransport is StreamableHttpClientSessionTransport)
        {
            // If this is our first attempt and we're using Streamable HTTP, try to fall back to SSE
            _logger.LogDebug(ex, "Streamable HTTP transport failed for {EndpointName}, attempting fallback to SSE transport", Name);
            
            // Stop message forwarding from the failed transport
            await StopMessageForwardingAsync().ConfigureAwait(false);
            
            // Dispose the failed transport
            await _activeTransport.DisposeAsync().ConfigureAwait(false);
            _activeTransport = null;
            _hasAttemptedStreamableHttp = true;
            
            // Try SSE transport
            await EnsureConnectedAsync(cancellationToken).ConfigureAwait(false);
            await _activeTransport!.SendMessageAsync(message, cancellationToken).ConfigureAwait(false);
        }
    }

    private async Task EnsureConnectedAsync(CancellationToken cancellationToken)
    {
        if (_activeTransport != null)
        {
            return;
        }

        if (!_hasAttemptedStreamableHttp)
        {
            // Try Streamable HTTP first
            _activeTransport = new StreamableHttpClientSessionTransport(_options, _httpClient, _loggerFactory, Name);
            SetConnected();
        }
        else
        {
            // Fall back to SSE
            var sseTransport = new SseClientSessionTransport(_options, _httpClient, _loggerFactory, Name);
            try
            {
                await sseTransport.ConnectAsync(cancellationToken).ConfigureAwait(false);
                _activeTransport = sseTransport;
                SetConnected();
            }
            catch
            {
                await sseTransport.DisposeAsync().ConfigureAwait(false);
                throw;
            }
        }

        // Start forwarding messages from the active transport
        StartMessageForwarding();
    }

    private void StartMessageForwarding()
    {
        if (_activeTransport == null)
        {
            return;
        }

        _messageForwardingCts = new CancellationTokenSource();
        _messageForwardingTask = ForwardMessagesAsync(_activeTransport.MessageReader, _messageForwardingCts.Token);
    }

    private async Task StopMessageForwardingAsync()
    {
        if (_messageForwardingCts != null)
        {
            await _messageForwardingCts.CancelAsync().ConfigureAwait(false);
        }

        if (_messageForwardingTask != null)
        {
            try
            {
                await _messageForwardingTask.ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                // Expected when cancelling
            }
        }

        _messageForwardingCts?.Dispose();
        _messageForwardingCts = null;
        _messageForwardingTask = null;
    }

    private async Task ForwardMessagesAsync(System.Threading.Channels.ChannelReader<JsonRpcMessage> reader, CancellationToken cancellationToken)
    {
        try
        {
            await foreach (var message in reader.ReadAllAsync(cancellationToken).ConfigureAwait(false))
            {
                WriteMessage(message);
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            // Expected when cancelling
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error forwarding messages from active transport for {EndpointName}", Name);
        }
    }

    public override async ValueTask DisposeAsync()
    {
        try
        {
            await StopMessageForwardingAsync().ConfigureAwait(false);
            
            if (_activeTransport != null)
            {
                await _activeTransport.DisposeAsync().ConfigureAwait(false);
            }
        }
        finally
        {
            SetDisconnected();
        }
    }
}