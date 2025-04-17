using ModelContextProtocol.Protocol.Messages;
using ModelContextProtocol.Utils;
using ModelContextProtocol.Utils.Json;
using System.Buffers;
using System.IO.Pipelines;
using System.Net.ServerSentEvents;
using System.Text.Json;
using System.Threading.Channels;

namespace ModelContextProtocol.Protocol.Transport;

/// <summary>
/// Provides an <see cref="ITransport"/> implementation using Server-Sent Events (SSE) for server-to-client communication.
/// </summary>
/// <remarks>
/// <para>
/// This transport provides one-way communication from server to client using the SSE protocol over HTTP,
/// while receiving client messages through a separate mechanism. It writes messages as 
/// SSE events to a response stream, typically associated with an HTTP response.
/// </para>
/// <para>
/// This transport is used in scenarios where the server needs to push messages to the client in real-time,
/// such as when streaming completion results or providing progress updates during long-running operations.
/// </para>
/// </remarks>
/// <param name="streamableHttpResponseBody">The response stream to write MCP JSON-RPC messages as SSE events to.</param>
public sealed class StreamableHttpServerTransport(PipeWriter streamableHttpResponseBody) : ITransport
{
    private readonly Channel<IJsonRpcMessage> _incomingChannel = CreateBoundedChannel<IJsonRpcMessage>();
    private readonly Channel<SseItem<IJsonRpcMessage>> _outgoingSseChannel = CreateBoundedChannel<SseItem<IJsonRpcMessage>>();

    private Task? _sseWriteTask;
    private Utf8JsonWriter? _jsonWriter;
    private bool _isConnected;

    /// <summary>
    /// Starts the transport and writes the JSON-RPC messages sent via <see cref="SendMessageAsync"/>
    /// to the SSE response stream until cancellation is requested or the transport is disposed.
    /// </summary>
    /// <param name="cancellationToken">The <see cref="CancellationToken"/> to monitor for cancellation requests. The default is <see cref="CancellationToken.None"/>.</param>
    /// <returns>A task representing the send loop that writes JSON-RPC messages to the SSE response stream.</returns>
    public Task RunAsync(CancellationToken cancellationToken)
    {
        _isConnected = true;

        var sseItems = _outgoingSseChannel.Reader.ReadAllAsync(cancellationToken);
        return _sseWriteTask = SseFormatter.WriteAsync(sseItems, streamableHttpResponseBody.AsStream(), WriteJsonRpcMessageToBuffer, cancellationToken);
    }

    private void WriteJsonRpcMessageToBuffer(SseItem<IJsonRpcMessage> item, IBufferWriter<byte> writer)
    {
        JsonSerializer.Serialize(GetUtf8JsonWriter(writer), item.Data, McpJsonUtilities.JsonContext.Default.IJsonRpcMessage);
    }

    /// <inheritdoc/>
    public ChannelReader<IJsonRpcMessage> MessageReader => _incomingChannel.Reader;

    /// <inheritdoc/>
    public ValueTask DisposeAsync()
    {
        _isConnected = false;
        _incomingChannel.Writer.TryComplete();
        _outgoingSseChannel.Writer.TryComplete();
        return new ValueTask(_sseWriteTask ?? Task.CompletedTask);
    }

    /// <inheritdoc/>
    public async Task SendMessageAsync(IJsonRpcMessage message, CancellationToken cancellationToken = default)
    {
        Throw.IfNull(message);

        if (!_isConnected)
        {
            throw new InvalidOperationException($"Transport is not connected. Make sure to call {nameof(RunAsync)} first.");
        }

        // Emit redundant "event: message" lines for better compatibility with other SDKs.
        await _outgoingSseChannel.Writer.WriteAsync(new SseItem<IJsonRpcMessage>(message, SseParser.EventTypeDefault), cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Handles incoming JSON-RPC messages received in POST bodies for the Streamable HTTP transport.
    /// </summary>
    /// <param name="streamableHttpRequestBody">The request body stream containing a JSON-RPC message or batched messages received from the client.</param>
    /// <param name="cancellationToken">The <see cref="CancellationToken"/> to monitor for cancellation requests. The default is <see cref="CancellationToken.None"/>.</param>
    /// <returns>A task representing the asynchronous operation to buffer the JSON-RPC message for processing.</returns>
    /// <exception cref="InvalidOperationException">Thrown when there is an attempt to process a message before calling <see cref="RunAsync(CancellationToken)"/>.</exception>
    /// <remarks>
    /// <para>
    /// This method is the entry point for processing client-to-server communication in the SSE transport model. 
    /// While the SSE protocol itself is unidirectional (server to client), this method allows bidirectional 
    /// communication by handling HTTP POST requests.
    /// </para>
    /// <para>
    /// When a client sends a JSON-RPC message in POST bodies, the server calls this method to
    /// process the message and make it available to the MCP server via the <see cref="MessageReader"/> channel.
    /// </para>
    /// <para>
    /// This method validates that the transport is connected before processing the message, ensuring proper
    /// sequencing of operations in the transport lifecycle.
    /// </para>
    /// </remarks>
    public async Task OnPostBodyReceivedAsync(PipeReader streamableHttpRequestBody, CancellationToken cancellationToken)
    {
        if (!_isConnected)
        {
            throw new InvalidOperationException($"Transport is not connected. Make sure to call {nameof(RunAsync)} first.");
        }

        if (!await IsJsonArrayAsync(streamableHttpRequestBody, cancellationToken).ConfigureAwait(false))
        {
            var message = await JsonSerializer.DeserializeAsync(streamableHttpRequestBody.AsStream(), McpJsonUtilities.JsonContext.Default.IJsonRpcMessage, cancellationToken).ConfigureAwait(false);
            await OnMessageReceivedAsync(message, cancellationToken).ConfigureAwait(false);
        }
        else
        {
            // Batched JSON-RPC message
            var messages = JsonSerializer.DeserializeAsyncEnumerable(streamableHttpRequestBody.AsStream(), McpJsonUtilities.JsonContext.Default.IJsonRpcMessage, cancellationToken).ConfigureAwait(false);
            await foreach (var message in messages.WithCancellation(cancellationToken))
            {
                await OnMessageReceivedAsync(message, cancellationToken).ConfigureAwait(false);
            }
        }
    }

    private async Task OnMessageReceivedAsync(IJsonRpcMessage? message, CancellationToken cancellationToken)
    {
        if (!_isConnected)
        {
            throw new InvalidOperationException($"Transport is not connected. Make sure to call {nameof(RunAsync)} first.");
        }

        if (message is null)
        {
            throw new McpException("Received invalid null message.");
        }

        if (message is JsonRpcRequest request)
        {
            request.SourceTransport = this;
        }

        await _incomingChannel.Writer.WriteAsync(message, cancellationToken).ConfigureAwait(false);
    }

    private async ValueTask<bool> IsJsonArrayAsync(PipeReader requestBody, CancellationToken cancellationToken)
    {
        // REVIEW: Should we bother trimming whitespace before checking for '['?
        var firstCharacterResult = await requestBody.ReadAtLeastAsync(1, cancellationToken).ConfigureAwait(false);

        try
        {
            if (firstCharacterResult.Buffer.Length == 0)
            {
                return false;
            }

            Span<byte> firstCharBuffer = stackalloc byte[1];
            firstCharacterResult.Buffer.Slice(0, 1).CopyTo(firstCharBuffer);
            return firstCharBuffer[0] == (byte)'[';
        }
        finally
        {
            // Never consume data when checking for '['. System.Text.Json still needs to consume it.
            requestBody.AdvanceTo(firstCharacterResult.Buffer.Start);
        }
    }

    private static Channel<T> CreateBoundedChannel<T>(int capacity = 1) =>
        Channel.CreateBounded<T>(new BoundedChannelOptions(capacity)
        {
            SingleReader = true,
            SingleWriter = false,
        });

    private Utf8JsonWriter GetUtf8JsonWriter(IBufferWriter<byte> writer)
    {
        if (_jsonWriter is null)
        {
            _jsonWriter = new Utf8JsonWriter(writer);
        }
        else
        {
            _jsonWriter.Reset(writer);
        }

        return _jsonWriter;
    }
}
