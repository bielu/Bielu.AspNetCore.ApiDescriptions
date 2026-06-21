using ByteBard.AsyncAPI.Bindings;
using ByteBard.AsyncAPI.Models;
using ByteBard.AsyncAPI.Readers;
using ByteBard.AsyncAPI.Readers.ParseNodes;
using ByteBard.AsyncAPI.Writers;

namespace Bielu.AspNetCore.AsyncApi.Extensions.Protocols.SignalR;

/// <summary>
/// Message binding for the <c>signalr</c> protocol. Describes how a message is framed on the wire
/// using a SignalR hub protocol.
/// </summary>
public class SignalRMessageBinding : MessageBinding<SignalRMessageBinding>
{
    /// <summary>The hub protocol used to serialize the message (see <see cref="SignalRProtocol.HubProtocols"/>).</summary>
    public string? HubProtocol { get; set; }

    /// <summary>The SignalR frame type (see <see cref="SignalRMessageType"/>).</summary>
    public SignalRMessageType? MessageType { get; set; }

    /// <summary>Schema describing optional message headers carried with the frame.</summary>
    public AsyncApiJsonSchema? Headers { get; set; }

    /// <inheritdoc />
    public override string BindingKey => SignalRProtocol.ProtocolName;

    /// <inheritdoc />
    protected override FixedFieldMap<SignalRMessageBinding> FixedFieldMap => new()
    {
        { "bindingVersion", (a, n) => a.BindingVersion = n.GetScalarValue() },
        { "hubProtocol", (a, n) => a.HubProtocol = n.GetScalarValue() },
        { "messageType", (a, n) => a.MessageType = SignalRMessageTypeExtensions.Parse(n.GetScalarValue()) },
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
        writer.WriteOptionalProperty("hubProtocol", HubProtocol);
        writer.WriteOptionalProperty("messageType", MessageType?.ToWireName());
        writer.WriteOptionalObject("headers", Headers, (w, s) => SignalRBindingSerializer.WriteSchema(w, s!, useV2Schema));
        writer.WriteOptionalProperty("bindingVersion", BindingVersion ?? SignalRProtocol.DefaultBindingVersion);
        writer.WriteExtensions(Extensions);
        writer.WriteEndObject();
    }
}
