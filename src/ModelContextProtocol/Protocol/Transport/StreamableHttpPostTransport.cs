using Microsoft.Extensions.AI;
using ModelContextProtocol.Protocol.Messages;
using ModelContextProtocol.Utils;
using ModelContextProtocol.Utils.Json;
using System.Buffers;
using System.Collections.Concurrent;
using System.IO.Pipelines;
using System.Text.Json;
using System.Threading.Channels;

namespace ModelContextProtocol.Protocol.Transport;

/// <summary>
/// Handles processing the request/response body pairs for the Streamable HTTP transport. This is typically used via
/// <see cref="JsonRpcMessage.RelatedTransport"/>.
/// </summary>
internal sealed class StreamableHttpPostTransport(ChannelWriter<JsonRpcMessage>? incomingChannel, IDuplexPipe httpBodies) : ITransport
{
    private readonly SseWriter _sseWriter = new();
    private readonly ConcurrentDictionary<RequestId, JsonRpcRequest> _pendingRequests = [];

    private Task? _sseWriteTask;

    /// <inheritdoc/>
    // REVIEW: Should we introduce a send-only interface for RelatedTransport?
    public ChannelReader<JsonRpcMessage> MessageReader => throw new NotSupportedException("JsonRpcMessage.RelatedTransport should only be used for sending messages.");

    /// <summary>
    /// Starts the transport and writes the JSON-RPC messages sent via <see cref="SendMessageAsync"/>
    /// to the SSE response stream until cancellation is requested or the transport is disposed.
    /// </summary>
    /// <param name="cancellationToken">The <see cref="CancellationToken"/> to monitor for cancellation requests. The default is <see cref="CancellationToken.None"/>.</param>
    /// <returns>A task representing the send loop that writes JSON-RPC messages to the SSE response stream.</returns>
    public async ValueTask<bool> RunAsync(CancellationToken cancellationToken)
    {
        // The incomingChannel is null to handle the potential client GET request to handle unsolicited JsonRpcMessages.
        if (incomingChannel is not null)
        {
            // Full duplex messages are not supported by the Streamable HTTP spec, but it would be easy for us to support
            // by running OnPostBodyReceivedAsync in parallel to the response writing loop in HandleSseRequestAsync.
            await OnPostBodyReceivedAsync(httpBodies.Input, cancellationToken).ConfigureAwait(false);
        }

        if (_pendingRequests.IsEmpty)
        {
            // No requests were received, so we don't need to write anything to the SSE stream.
            return false;
        }

        _sseWriteTask = _sseWriter.WriteAllAsync(httpBodies.Output.AsStream(), cancellationToken);
        await _sseWriteTask.ConfigureAwait(false);
        return true;
    }

    /// <inheritdoc/>
    public async Task SendMessageAsync(JsonRpcMessage message, CancellationToken cancellationToken = default)
    {
        if (message is JsonRpcResponse response)
        {
            if (_pendingRequests.TryRemove(response.Id, out _) && _pendingRequests.IsEmpty)
            {
                // Complete the SSE response stream.
                _sseWriter.Dispose();
            }
        }
        await _sseWriter.SendMessageAsync(message, cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc/>
    public ValueTask DisposeAsync()
    {
        _sseWriter.Dispose();
        return new ValueTask(_sseWriteTask ?? Task.CompletedTask);
    }

    /// <summary>
    /// Handles incoming JSON-RPC messages received in POST bodies for the Streamable HTTP transport.
    /// </summary>
    /// <param name="streamableHttpRequestBody">The request body containing a JSON-RPC message or batched messages received from the client.</param>
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
    private async ValueTask OnPostBodyReceivedAsync(PipeReader streamableHttpRequestBody, CancellationToken cancellationToken)
    {
        if (!await IsJsonArrayAsync(streamableHttpRequestBody, cancellationToken).ConfigureAwait(false))
        {
            var message = await JsonSerializer.DeserializeAsync(streamableHttpRequestBody.AsStream(), McpJsonUtilities.JsonContext.Default.JsonRpcMessage, cancellationToken).ConfigureAwait(false);
            await OnMessageReceivedAsync(message, cancellationToken).ConfigureAwait(false);
        }
        else
        {
            // Batched JSON-RPC message
            var messages = JsonSerializer.DeserializeAsyncEnumerable(streamableHttpRequestBody.AsStream(), McpJsonUtilities.JsonContext.Default.JsonRpcMessage, cancellationToken).ConfigureAwait(false);
            await foreach (var message in messages.WithCancellation(cancellationToken))
            {
                await OnMessageReceivedAsync(message, cancellationToken).ConfigureAwait(false);
            }
        }
    }

    private async ValueTask OnMessageReceivedAsync(JsonRpcMessage? message, CancellationToken cancellationToken)
    {
        if (message is null)
        {
            throw new McpException("Received invalid null message.");
        }

        if (message is JsonRpcRequest request)
        {
            _pendingRequests[request.Id] = request;
        }

        message.RelatedTransport = this;

        // Really an assertion. This doesn't get called when incomingChannel is null for GET requests.
        Throw.IfNull(incomingChannel);
        await incomingChannel.WriteAsync(message, cancellationToken).ConfigureAwait(false);
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
}
