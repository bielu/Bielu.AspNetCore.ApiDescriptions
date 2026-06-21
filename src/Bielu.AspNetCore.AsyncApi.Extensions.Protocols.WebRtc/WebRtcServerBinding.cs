using ByteBard.AsyncAPI.Bindings;
using ByteBard.AsyncAPI.Readers.ParseNodes;
using ByteBard.AsyncAPI.Writers;

namespace Bielu.AspNetCore.AsyncApi.Extensions.Protocols.WebRtc;

/// <summary>
/// Server binding for the <c>webrtc</c> protocol. Describes the signaling endpoint and ICE
/// configuration a peer needs to establish a connection.
/// </summary>
public class WebRtcServerBinding : ServerBinding<WebRtcServerBinding>
{
    /// <summary>The URL of the signaling channel used to exchange SDP and ICE candidates.</summary>
    public string? SignalingUrl { get; set; }

    /// <summary>STUN/TURN server URLs the peer should use for ICE (for example <c>stun:stun.l.google.com:19302</c>).</summary>
    public IList<string> IceServers { get; set; } = new List<string>();

    /// <summary>The bundle policy advertised by the endpoint (see <see cref="WebRtcProtocol.BundlePolicies"/>).</summary>
    public string? BundlePolicy { get; set; }

    /// <inheritdoc />
    public override string BindingKey => WebRtcProtocol.ProtocolName;

    /// <inheritdoc />
    protected override FixedFieldMap<WebRtcServerBinding> FixedFieldMap => new()
    {
        { "bindingVersion", (a, n) => a.BindingVersion = n.GetScalarValue() },
        { "signalingUrl", (a, n) => a.SignalingUrl = n.GetScalarValue() },
        { "iceServers", (a, n) => a.IceServers = n.CreateSimpleList(s => s.GetScalarValue()) },
        { "bundlePolicy", (a, n) => a.BundlePolicy = n.GetScalarValue() },
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
        writer.WriteOptionalProperty("signalingUrl", SignalingUrl);
        writer.WriteOptionalCollection("iceServers", IceServers, (w, v) => w.WriteValue(v));
        writer.WriteOptionalProperty("bundlePolicy", BundlePolicy);
        writer.WriteOptionalProperty("bindingVersion", BindingVersion ?? WebRtcProtocol.DefaultBindingVersion);
        writer.WriteExtensions(Extensions);
        writer.WriteEndObject();
    }
}
