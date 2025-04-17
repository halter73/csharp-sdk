using ModelContextProtocol.Protocol.Transport;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;

namespace ModelContextProtocol.Protocol.Messages;

/// <summary>
/// A request message in the JSON-RPC protocol.
/// </summary>
/// <remarks>
/// Requests are messages that require a response from the receiver. Each request includes a unique ID
/// that will be included in the corresponding response message (either a success response or an error).
/// 
/// The receiver of a request message is expected to execute the specified method with the provided parameters
/// and return either a <see cref="JsonRpcResponse"/> with the result, or a <see cref="JsonRpcError"/>
/// if the method execution fails.
/// </remarks>
public record JsonRpcRequest : IJsonRpcMessageWithId
{
    /// <inheritdoc />
    [JsonPropertyName("jsonrpc")]
    public string JsonRpc { get; init; } = "2.0";

    /// <inheritdoc/>
    [JsonPropertyName("id")]
    public RequestId Id { get; set; }

    /// <summary>
    /// Name of the method to invoke.
    /// </summary>
    [JsonPropertyName("method")]
    public required string Method { get; init; }

    /// <summary>
    /// Optional parameters for the method.
    /// </summary>
    [JsonPropertyName("params")]
    public JsonNode? Params { get; init; }

    // Used internally to support Streamable HTTP scenarios where the spec states that the server SHOULD
    // send JSON-RPC responses as part of the HTTP response to the POST that included the JSON-RPC request.
    internal ITransport? SourceTransport { get; set; }
}
