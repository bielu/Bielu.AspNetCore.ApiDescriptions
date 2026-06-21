namespace Bielu.AspNetCore.AsyncApi.Extensions.Protocols.WebRtc;

/// <summary>
/// Well-known constants for the custom <c>webrtc</c> AsyncAPI protocol.
/// </summary>
/// <remarks>
/// WebRTC is not part of the official AsyncAPI bindings catalogue, so these values define a
/// project-specific binding shape. They are intentionally aligned with the concepts exposed by the
/// W3C WebRTC API: peer connections established through SDP offer/answer signaling and ICE candidate
/// exchange, carrying media tracks or <c>RTCDataChannel</c> streams.
/// </remarks>
public static class WebRtcProtocol
{
    /// <summary>The protocol identifier used in server and binding keys.</summary>
    public const string ProtocolName = "webrtc";

    /// <summary>The default binding version emitted when none is set explicitly.</summary>
    public const string DefaultBindingVersion = "0.1.0";

    /// <summary>The kinds of channel a WebRTC peer connection can carry.</summary>
    public static class ChannelTypes
    {
        /// <summary>An <c>RTCDataChannel</c> carrying arbitrary application data.</summary>
        public const string DataChannel = "dataChannel";

        /// <summary>A media track (audio or video).</summary>
        public const string Media = "media";
    }

    /// <summary>How a data-channel payload is encoded on the wire.</summary>
    public static class Encodings
    {
        public const string Text = "text";
        public const string Binary = "binary";
        public const string Json = "json";
    }

    /// <summary>Direction of a message relative to the local peer.</summary>
    public static class Directions
    {
        public const string ClientToServer = "clientToServer";
        public const string ServerToClient = "serverToClient";
        public const string Bidirectional = "bidirectional";
    }

    /// <summary>Bundle policies a peer connection can negotiate (mirrors <c>RTCBundlePolicy</c>).</summary>
    public static class BundlePolicies
    {
        public const string Balanced = "balanced";
        public const string MaxCompat = "max-compat";
        public const string MaxBundle = "max-bundle";
    }
}

/// <summary>
/// The signaling messages exchanged to establish a WebRTC peer connection.
/// See https://www.w3.org/TR/webrtc/#session-description-model.
/// </summary>
public enum WebRtcSignalingType
{
    /// <summary>An SDP offer initiating the negotiation.</summary>
    Offer,

    /// <summary>An SDP answer responding to an offer.</summary>
    Answer,

    /// <summary>An ICE candidate discovered during connectivity checks.</summary>
    Candidate,
}

/// <summary>Serialization helpers mapping <see cref="WebRtcSignalingType"/> to/from its wire token.</summary>
public static class WebRtcSignalingTypeExtensions
{
    /// <summary>
    /// Returns the camelCase token used in the binding document, for example
    /// <see cref="WebRtcSignalingType.Candidate"/> becomes <c>"candidate"</c>.
    /// </summary>
    public static string ToWireName(this WebRtcSignalingType type)
    {
        var name = type.ToString();
        return char.ToLowerInvariant(name[0]) + name[1..];
    }

    /// <summary>
    /// Parses a signaling type from its camelCase token (for example <c>"offer"</c>). The match is
    /// case-insensitive. Returns <see langword="null"/> when the value is missing or unrecognized.
    /// </summary>
    public static WebRtcSignalingType? Parse(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        return Enum.TryParse<WebRtcSignalingType>(value, ignoreCase: true, out var parsed) && Enum.IsDefined(parsed)
            ? parsed
            : null;
    }
}
