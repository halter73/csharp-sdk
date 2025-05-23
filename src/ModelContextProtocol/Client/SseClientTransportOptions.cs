namespace ModelContextProtocol.Client;

/// <summary>
/// Provides options for configuring <see cref="SseClientTransport"/> instances.
/// </summary>
public record SseClientTransportOptions
{
    /// <summary>
    /// Gets or sets the base address of the server for SSE connections.
    /// </summary>
    public required Uri Endpoint
    {
        get;
        init
        {
            if (value is null)
            {
                throw new ArgumentNullException(nameof(value), "Endpoint cannot be null.");
            }
            if (!value.IsAbsoluteUri)
            {
                throw new ArgumentException("Endpoint must be an absolute URI.", nameof(value));
            }
            if (value.Scheme != Uri.UriSchemeHttp && value.Scheme != Uri.UriSchemeHttps)
            {
                throw new ArgumentException("Endpoint must use HTTP or HTTPS scheme.", nameof(value));
            }

            field = value;
        }
    }

    /// <summary>
    /// Gets or sets a value indicating whether to use "Streamable HTTP" for the transport rather than "HTTP with SSE". Defaults to false.
    /// <see href="https://modelcontextprotocol.io/specification/2025-03-26/basic/transports#streamable-http">Streamable HTTP transport specification</see>.
    /// <see href="https://modelcontextprotocol.io/specification/2024-11-05/basic/transports#http-with-sse">HTTP with SSE transport specification</see>.
    /// </summary>
    /// <remarks>
    /// This property is maintained for backward compatibility. Consider using <see cref="TransportMode"/> instead.
    /// When <see cref="TransportMode"/> is not explicitly set, this property determines the transport mode:
    /// <see langword="true"/> maps to <see cref="SseTransportMode.StreamableHttp"/>, 
    /// <see langword="false"/> maps to <see cref="SseTransportMode.AutoDetect"/>.
    /// </remarks>
    public bool UseStreamableHttp { get; init; }

    /// <summary>
    /// Gets or sets the transport mode to use for the connection. Defaults to <see cref="SseTransportMode.AutoDetect"/>.
    /// </summary>
    /// <remarks>
    /// <para>
    /// When set to <see cref="SseTransportMode.AutoDetect"/> (the default), the client will first attempt to use
    /// Streamable HTTP transport and automatically fall back to SSE transport if the server doesn't support it.
    /// This provides the best compatibility and matches the behavior of VS Code.
    /// </para>
    /// <para>
    /// When this property is explicitly set, it takes precedence over <see cref="UseStreamableHttp"/>.
    /// </para>
    /// </remarks>
    public SseTransportMode? TransportMode { get; init; }

    /// <summary>
    /// Gets a transport identifier used for logging purposes.
    /// </summary>
    public string? Name { get; init; }

    /// <summary>
    /// Gets or sets a timeout used to establish the initial connection to the SSE server. Defaults to 30 seconds.
    /// </summary>
    /// <remarks>
    /// This timeout controls how long the client waits for:
    /// <list type="bullet">
    ///   <item><description>The initial HTTP connection to be established with the SSE server</description></item>
    ///   <item><description>The endpoint event to be received, which indicates the message endpoint URL</description></item>
    /// </list>
    /// If the timeout expires before the connection is established, a <see cref="TimeoutException"/> will be thrown.
    /// </remarks>
    public TimeSpan ConnectionTimeout { get; init; } = TimeSpan.FromSeconds(30);

    /// <summary>
    /// Gets custom HTTP headers to include in requests to the SSE server.
    /// </summary>
    /// <remarks>
    /// Use this property to specify custom HTTP headers that should be sent with each request to the server.
    /// </remarks>
    public Dictionary<string, string>? AdditionalHeaders { get; init; }

    /// <summary>
    /// Gets the effective transport mode based on the current configuration.
    /// </summary>
    /// <returns>The transport mode to use for the connection.</returns>
    internal SseTransportMode GetEffectiveTransportMode()
    {
        // If TransportMode is explicitly set, use it
        if (TransportMode.HasValue)
        {
            return TransportMode.Value;
        }

        // Fall back to UseStreamableHttp for backward compatibility
        return UseStreamableHttp ? SseTransportMode.StreamableHttp : SseTransportMode.AutoDetect;
    }
}