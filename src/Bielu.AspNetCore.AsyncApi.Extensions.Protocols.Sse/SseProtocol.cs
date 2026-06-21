namespace Bielu.AspNetCore.AsyncApi.Extensions.Protocols.Sse;

/// <summary>
/// Well-known constants for the custom <c>sse</c> AsyncAPI protocol.
/// </summary>
/// <remarks>
/// Server-Sent Events are not part of the official AsyncAPI bindings catalogue, so these values
/// define a project-specific binding shape. They are intentionally aligned with the concepts exposed
/// by the WHATWG EventSource / <c>text/event-stream</c> specification (a one-way server-to-client
/// stream of <c>event</c>/<c>data</c>/<c>id</c>/<c>retry</c> frames delivered over HTTP).
/// </remarks>
public static class SseProtocol
{
    /// <summary>The protocol identifier used in server and binding keys.</summary>
    public const string ProtocolName = "sse";

    /// <summary>The default binding version emitted when none is set explicitly.</summary>
    public const string DefaultBindingVersion = "0.1.0";

    /// <summary>The media type an SSE endpoint streams.</summary>
    public const string EventStreamContentType = "text/event-stream";

    /// <summary>HTTP methods an SSE endpoint can be opened with (EventSource always uses GET).</summary>
    public static class Methods
    {
        public const string Get = "GET";
    }

    /// <summary>Direction of an SSE message relative to the server. SSE is server-to-client only.</summary>
    public static class Directions
    {
        /// <summary>The server pushes events to the connected client.</summary>
        public const string ServerToClient = "serverToClient";
    }
}
