using ModelContextProtocol.Protocol.Messages;
using ModelContextProtocol.Utils;
using ModelContextProtocol.Utils.Json;
using System.Buffers;
using System.Net.ServerSentEvents;
using System.Text;
using System.Text.Json;
using System.Threading.Channels;

namespace ModelContextProtocol.Protocol.Transport;

internal sealed class SseWriter(string? messageEndpoint = null, BoundedChannelOptions? channelOptions = null) : IDisposable
{
    private readonly Channel<SseItem<JsonRpcMessage?>> _messages = Channel.CreateBounded<SseItem<JsonRpcMessage?>>(channelOptions ?? new BoundedChannelOptions(1)
    {
        SingleReader = true,
        SingleWriter = false,
    });

    private Utf8JsonWriter? _jsonWriter;

    public Task WriteAllAsync(Stream sseResponseStream, CancellationToken cancellationToken)
    {
        // When messageEndpoint is set, the very first SSE event isn't really an IJsonRpcMessage, but there's no API to write a single
        // item of a different type, so we fib and special-case the "endpoint" event type in the formatter.
        if (messageEndpoint is not null && !_messages.Writer.TryWrite(new SseItem<JsonRpcMessage?>(null, "endpoint")))
        {
            throw new InvalidOperationException($"You must call ${nameof(WriteAllAsync)} before calling ${nameof(SendMessageAsync)}.");
        }

        return SseFormatter.WriteAsync(_messages.Reader.ReadAllAsync(cancellationToken), sseResponseStream, WriteJsonRpcMessageToBuffer, cancellationToken);
    }

    public async Task SendMessageAsync(JsonRpcMessage message, CancellationToken cancellationToken = default)
    {
        Throw.IfNull(message);

        // Emit redundant "event: message" lines for better compatibility with other SDKs.
        await _messages.Writer.WriteAsync(new SseItem<JsonRpcMessage?>(message, SseParser.EventTypeDefault), cancellationToken).ConfigureAwait(false);
    }

    public void Dispose()
    {
        _messages.Writer.TryComplete();
        _jsonWriter?.Dispose();
    }

    private void WriteJsonRpcMessageToBuffer(SseItem<JsonRpcMessage?> item, IBufferWriter<byte> writer)
    {
        if (item.EventType == "endpoint" && messageEndpoint is not null)
        {
            writer.Write(Encoding.UTF8.GetBytes(messageEndpoint));
            return;
        }

        JsonSerializer.Serialize(GetUtf8JsonWriter(writer), item.Data, McpJsonUtilities.JsonContext.Default.JsonRpcMessage!);
    }

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
