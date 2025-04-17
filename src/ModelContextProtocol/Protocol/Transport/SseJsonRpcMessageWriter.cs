using ModelContextProtocol.Protocol.Messages;
using ModelContextProtocol.Utils.Json;
using System.Buffers;
using System.Net.ServerSentEvents;
using System.Text;
using System.Text.Json;
using System.Threading.Channels;

namespace ModelContextProtocol.Protocol.Transport;

internal sealed class SseJsonRpcMessageWriter(Stream sseResponseStream, string messageEndpoint = "/message")
{
    private Utf8JsonWriter? _jsonWriter;

    public Task WriteAsync(ChannelReader<SseItem<JsonRpcMessage?>> messages, CancellationToken cancellationToken)
    {
        var sseItems = messages.ReadAllAsync(cancellationToken);
        return SseFormatter.WriteAsync(sseItems, sseResponseStream, WriteJsonRpcMessageToBuffer, cancellationToken);
    }

    private void WriteJsonRpcMessageToBuffer(SseItem<JsonRpcMessage?> item, IBufferWriter<byte> writer)
    {
        if (item.EventType == "endpoint")
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
