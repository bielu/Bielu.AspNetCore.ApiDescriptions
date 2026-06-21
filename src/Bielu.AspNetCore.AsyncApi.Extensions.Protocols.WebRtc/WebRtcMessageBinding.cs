using ByteBard.AsyncAPI.Bindings;
using ByteBard.AsyncAPI.Models;
using ByteBard.AsyncAPI.Readers;
using ByteBard.AsyncAPI.Readers.ParseNodes;
using ByteBard.AsyncAPI.Writers;

namespace Bielu.AspNetCore.AsyncApi.Extensions.Protocols.WebRtc;

/// <summary>
/// Message binding for the <c>webrtc</c> protocol. Describes either a signaling message
/// (SDP offer/answer or ICE candidate) or how a data-channel payload is framed on the wire.
/// </summary>
public class WebRtcMessageBinding : MessageBinding<WebRtcMessageBinding>
{
    /// <summary>The signaling message kind, when the message is part of negotiation (see <see cref="WebRtcSignalingType"/>).</summary>
    public WebRtcSignalingType? SignalingType { get; set; }

    /// <summary>How the data-channel payload is encoded (see <see cref="WebRtcProtocol.Encodings"/>).</summary>
    public string? Encoding { get; set; }

    /// <summary>Schema describing optional metadata carried alongside the message.</summary>
    public AsyncApiJsonSchema? Headers { get; set; }

    /// <inheritdoc />
    public override string BindingKey => WebRtcProtocol.ProtocolName;

    /// <inheritdoc />
    protected override FixedFieldMap<WebRtcMessageBinding> FixedFieldMap => new()
    {
        { "bindingVersion", (a, n) => a.BindingVersion = n.GetScalarValue() },
        { "signalingType", (a, n) => a.SignalingType = WebRtcSignalingTypeExtensions.Parse(n.GetScalarValue()) },
        { "encoding", (a, n) => a.Encoding = n.GetScalarValue() },
        { "headers", (a, n) => a.Headers = AsyncApiJsonSchemaDeserializer.LoadSchema(n) },
    };

    /// <inheritdoc />
    public override void SerializeV2(IAsyncApiWriter writer) => Serialize(writer, useV2Schema: true);

    /// <inheritdoc />
    public override void SerializeV3(IAsyncApiWriter writer) => Serialize(writer, useV2Schema: false);

    /// <inheritdoc />
    public override void SerializeProperties(IAsyncApiWriter writer) => Serialize(writer, useV2Schema: false);

    private void Serialize(IAsyncApiWriter writer, bool useV2Schema)
    {
        ArgumentNullException.ThrowIfNull(writer);

        writer.WriteStartObject();
        writer.WriteOptionalProperty("signalingType", SignalingType?.ToWireName());
        writer.WriteOptionalProperty("encoding", Encoding);
        writer.WriteOptionalObject("headers", Headers, (w, s) => WebRtcBindingSerializer.WriteSchema(w, s!, useV2Schema));
        writer.WriteOptionalProperty("bindingVersion", BindingVersion ?? WebRtcProtocol.DefaultBindingVersion);
        writer.WriteExtensions(Extensions);
        writer.WriteEndObject();
    }
}
