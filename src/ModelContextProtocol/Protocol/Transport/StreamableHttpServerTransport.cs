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
public sealed class StreamableHttpServerTransport : ITransport
{
    private readonly Channel<JsonRpcMessage> _incomingChannel = SseResponseStreamTransport.CreateBoundedChannel<JsonRpcMessage>();
    // For JsonRpcMessages without a RelatedTransport, we don't want a block just because the client didn't make a GET request to handle unsolicited messages.
    private readonly Channel<SseItem<JsonRpcMessage>> _outgoingSseChannel = SseResponseStreamTransport.CreateBoundedChannel<SseItem<JsonRpcMessage>>(fullMode: BoundedChannelFullMode.DropOldest);

    /// <summary>
    /// Starts the transport and writes the JSON-RPC messages sent via <see cref="SendMessageAsync"/>
    /// to the SSE response stream until cancellation is requested or the transport is disposed.
    /// </summary>
    /// <param name="cancellationToken">The <see cref="CancellationToken"/> to monitor for cancellation requests. The default is <see cref="CancellationToken.None"/>.</param>
    /// <returns>A task representing the send loop that writes JSON-RPC messages to the SSE response stream.</returns>
    public Task RunAsync(CancellationToken cancellationToken)
    {
    }

    /// <inheritdoc/>
    public ChannelReader<JsonRpcMessage> MessageReader => _incomingChannel.Reader;

    /// <inheritdoc/>
    public async Task SendMessageAsync(JsonRpcMessage message, CancellationToken cancellationToken = default)
    {
        Throw.IfNull(message);
        // Emit redundant "event: message" lines for better compatibility with other SDKs.
        await _outgoingSseChannel.Writer.WriteAsync(new SseItem<JsonRpcMessage>(message, SseParser.EventTypeDefault), cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc/>
    public ValueTask DisposeAsync()
    {
        _outgoingSseChannel.Writer.TryComplete();
        return default;
    }
}
