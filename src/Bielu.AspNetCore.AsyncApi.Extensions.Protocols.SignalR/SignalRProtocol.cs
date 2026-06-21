namespace Bielu.AspNetCore.AsyncApi.Extensions.Protocols.SignalR;

/// <summary>
/// Well-known constants for the custom <c>signalr</c> AsyncAPI protocol.
/// </summary>
/// <remarks>
/// SignalR is not part of the official AsyncAPI bindings catalogue, so these values define a
/// project-specific binding shape. They are intentionally aligned with the concepts exposed by
/// ASP.NET Core SignalR (hubs, transports, hub protocols and the JSON hub-protocol message frames).
/// </remarks>
public static class SignalRProtocol
{
    /// <summary>The protocol identifier used in server and binding keys.</summary>
    public const string ProtocolName = "signalr";

    /// <summary>The default binding version emitted when none is set explicitly.</summary>
    public const string DefaultBindingVersion = "0.1.0";

    /// <summary>Transports a SignalR connection can negotiate.</summary>
    public static class Transports
    {
        public const string WebSockets = "webSockets";
        public const string ServerSentEvents = "serverSentEvents";
        public const string LongPolling = "longPolling";
    }

    /// <summary>Hub protocols used to serialize SignalR messages.</summary>
    public static class HubProtocols
    {
        public const string Json = "json";
        public const string MessagePack = "messagepack";
    }

    /// <summary>Direction of a hub method relative to the server.</summary>
    public static class Directions
    {
        /// <summary>The client invokes a method on the server (a hub method).</summary>
        public const string ClientToServer = "clientToServer";

        /// <summary>The server pushes a message to connected clients.</summary>
        public const string ServerToClient = "serverToClient";
    }

    /// <summary>The kind of SignalR call an operation represents.</summary>
    public static class CallTypes
    {
        /// <summary>A single invocation that may return a result.</summary>
        public const string Invocation = "invocation";

        /// <summary>An invocation whose result is a stream of items.</summary>
        public const string StreamInvocation = "streamInvocation";

        /// <summary>A fire-and-forget invocation with no result.</summary>
        public const string Send = "send";
    }
}

/// <summary>
/// Message type identifiers defined by the SignalR hub protocol. The underlying integer values are
/// the wire identifiers used in the JSON/MessagePack hub protocols.
/// See https://github.com/dotnet/aspnetcore/blob/main/src/SignalR/docs/specs/HubProtocol.md.
/// </summary>
public enum SignalRMessageType
{
    Invocation = 1,
    StreamItem = 2,
    Completion = 3,
    StreamInvocation = 4,
    CancelInvocation = 5,
    Ping = 6,
    Close = 7,
}

/// <summary>Serialization helpers mapping <see cref="SignalRMessageType"/> to/from its wire token.</summary>
public static class SignalRMessageTypeExtensions
{
    /// <summary>
    /// Returns the camelCase token used in the binding document, for example
    /// <see cref="SignalRMessageType.StreamInvocation"/> becomes <c>"streamInvocation"</c>.
    /// </summary>
    public static string ToWireName(this SignalRMessageType type)
    {
        var name = type.ToString();
        return char.ToLowerInvariant(name[0]) + name[1..];
    }

    /// <summary>
    /// Parses a message type from its camelCase token (for example <c>"invocation"</c>). For backwards
    /// compatibility the numeric wire id (for example <c>"1"</c>) is also accepted. Returns
    /// <see langword="null"/> when the value is missing or unrecognized.
    /// </summary>
    public static SignalRMessageType? Parse(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        return Enum.TryParse<SignalRMessageType>(value, ignoreCase: true, out var parsed) && Enum.IsDefined(parsed)
            ? parsed
            : null;
    }
}
