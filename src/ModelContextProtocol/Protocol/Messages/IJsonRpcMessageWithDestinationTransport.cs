using ModelContextProtocol.Protocol.Transport;

namespace ModelContextProtocol.Protocol.Messages;

internal interface IJsonRpcMessageWithDestinationTransport
{
    /// <summary>
    /// Used internally to support Streamable HTTP scenarios where the spec states that the server SHOULD
    /// send JSON-RPC responses as part of the HTTP response to the POST that included the JSON-RPC request.
    /// </summary>
    ITransport? DestinationTransport { get; set; }
}
