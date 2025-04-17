using ModelContextProtocol.Protocol.Transport;
using ModelContextProtocol.Utils.Json;
using System.Text.Json.Serialization;

namespace ModelContextProtocol.Protocol.Messages;

/// <summary>
/// Represents any JSON-RPC message used in the Model Context Protocol (MCP).
/// </summary>
/// <remarks>
/// This interface serves as the foundation for all message types in the JSON-RPC 2.0 protocol
/// used by MCP, including requests, responses, notifications, and errors. JSON-RPC is a stateless,
/// lightweight remote procedure call (RPC) protocol that uses JSON as its data format.
/// </remarks>
[JsonConverter(typeof(JsonRpcMessageConverter))]
public abstract class JsonRpcMessage
{
    /// <summary>
    /// Gets the JSON-RPC protocol version used.
    /// </summary>
    /// <inheritdoc />
    [JsonPropertyName("jsonrpc")]
    public string JsonRpc { get; init; } = "2.0";

    /// <summary>
    /// If set, the transport the JsonRpcMessage was received on or should be sent over. This is used internally to support
    /// the Streamable HTTP transport where the spec states that the server SHOULD send JSON-RPC responses as part of the
    /// HTTP response to the POST that included the JSON-RPC request.
    /// </summary>
    [JsonIgnore]
    public ITransport? RelatedTransport { get; set; }
}
