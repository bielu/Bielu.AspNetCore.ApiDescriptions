using ByteBard.AsyncAPI.Bindings;
using ByteBard.AsyncAPI.Readers.ParseNodes;
using ByteBard.AsyncAPI.Writers;

namespace Bielu.AspNetCore.AsyncApi.Extensions.Protocols.WebRtc;

/// <summary>
/// Operation binding for the <c>webrtc</c> protocol. An operation maps to either a signaling exchange
/// (offer/answer/candidate) or a send/receive over an established data channel.
/// </summary>
public class WebRtcOperationBinding : OperationBinding<WebRtcOperationBinding>
{
    /// <summary>The signaling message this operation carries, when it is part of negotiation (see <see cref="WebRtcSignalingType"/>).</summary>
    public WebRtcSignalingType? SignalingType { get; set; }

    /// <summary>Direction of the message relative to the local peer (see <see cref="WebRtcProtocol.Directions"/>).</summary>
    public string? Direction { get; set; }

    /// <inheritdoc />
    public override string BindingKey => WebRtcProtocol.ProtocolName;

    /// <inheritdoc />
    protected override FixedFieldMap<WebRtcOperationBinding> FixedFieldMap => new()
    {
        { "bindingVersion", (a, n) => a.BindingVersion = n.GetScalarValue() },
        { "signalingType", (a, n) => a.SignalingType = WebRtcSignalingTypeExtensions.Parse(n.GetScalarValue()) },
        { "direction", (a, n) => a.Direction = n.GetScalarValue() },
    };

    /// <inheritdoc />
    public override void SerializeV2(IAsyncApiWriter writer) => Serialize(writer);

    /// <inheritdoc />
    public override void SerializeV3(IAsyncApiWriter writer) => Serialize(writer);

    /// <inheritdoc />
    public override void SerializeProperties(IAsyncApiWriter writer) => Serialize(writer);

    private void Serialize(IAsyncApiWriter writer)
    {
        ArgumentNullException.ThrowIfNull(writer);

        writer.WriteStartObject();
        writer.WriteOptionalProperty("signalingType", SignalingType?.ToWireName());
        writer.WriteOptionalProperty("direction", Direction);
        writer.WriteOptionalProperty("bindingVersion", BindingVersion ?? WebRtcProtocol.DefaultBindingVersion);
        writer.WriteExtensions(Extensions);
        writer.WriteEndObject();
    }
}
