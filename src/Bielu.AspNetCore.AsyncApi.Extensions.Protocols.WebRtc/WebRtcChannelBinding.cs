using ByteBard.AsyncAPI.Bindings;
using ByteBard.AsyncAPI.Readers.ParseNodes;
using ByteBard.AsyncAPI.Writers;

namespace Bielu.AspNetCore.AsyncApi.Extensions.Protocols.WebRtc;

/// <summary>
/// Channel binding for the <c>webrtc</c> protocol. A channel maps to a single <c>RTCDataChannel</c>
/// or media track carried by a peer connection.
/// </summary>
public class WebRtcChannelBinding : ChannelBinding<WebRtcChannelBinding>
{
    /// <summary>The kind of channel (see <see cref="WebRtcProtocol.ChannelTypes"/>).</summary>
    public string? ChannelType { get; set; }

    /// <summary>The data-channel label, for example <c>chat</c>.</summary>
    public string? Label { get; set; }

    /// <summary>The application sub-protocol negotiated for the data channel (the <c>protocol</c> field).</summary>
    public string? SubProtocol { get; set; }

    /// <summary>Whether messages are delivered in order (<c>RTCDataChannel.ordered</c>).</summary>
    public bool? Ordered { get; set; }

    /// <summary>Maximum number of retransmit attempts for an unreliable channel (<c>maxRetransmits</c>).</summary>
    public int? MaxRetransmits { get; set; }

    /// <summary>Maximum time in milliseconds to retransmit a message (<c>maxPacketLifeTime</c>).</summary>
    public int? MaxPacketLifeTime { get; set; }

    /// <summary>Whether the channel was negotiated out-of-band by the application (<c>negotiated</c>).</summary>
    public bool? Negotiated { get; set; }

    /// <summary>The channel id assigned when the channel is negotiated out-of-band (<c>id</c>).</summary>
    public int? Id { get; set; }

    /// <inheritdoc />
    public override string BindingKey => WebRtcProtocol.ProtocolName;

    /// <inheritdoc />
    protected override FixedFieldMap<WebRtcChannelBinding> FixedFieldMap => new()
    {
        { "bindingVersion", (a, n) => a.BindingVersion = n.GetScalarValue() },
        { "channelType", (a, n) => a.ChannelType = n.GetScalarValue() },
        { "label", (a, n) => a.Label = n.GetScalarValue() },
        { "subProtocol", (a, n) => a.SubProtocol = n.GetScalarValue() },
        { "ordered", (a, n) => a.Ordered = n.GetBooleanValue() },
        { "maxRetransmits", (a, n) => a.MaxRetransmits = ParseInt(n.GetScalarValue()) },
        { "maxPacketLifeTime", (a, n) => a.MaxPacketLifeTime = ParseInt(n.GetScalarValue()) },
        { "negotiated", (a, n) => a.Negotiated = n.GetBooleanValue() },
        { "id", (a, n) => a.Id = ParseInt(n.GetScalarValue()) },
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
        writer.WriteOptionalProperty("channelType", ChannelType);
        writer.WriteOptionalProperty("label", Label);
        writer.WriteOptionalProperty("subProtocol", SubProtocol);
        writer.WriteOptionalProperty("ordered", Ordered);
        writer.WriteOptionalProperty("maxRetransmits", MaxRetransmits);
        writer.WriteOptionalProperty("maxPacketLifeTime", MaxPacketLifeTime);
        writer.WriteOptionalProperty("negotiated", Negotiated);
        writer.WriteOptionalProperty("id", Id);
        writer.WriteOptionalProperty("bindingVersion", BindingVersion ?? WebRtcProtocol.DefaultBindingVersion);
        writer.WriteExtensions(Extensions);
        writer.WriteEndObject();
    }

    private static int? ParseInt(string? value) =>
        int.TryParse(value, System.Globalization.NumberStyles.Integer, System.Globalization.CultureInfo.InvariantCulture, out var parsed)
            ? parsed
            : null;
}
