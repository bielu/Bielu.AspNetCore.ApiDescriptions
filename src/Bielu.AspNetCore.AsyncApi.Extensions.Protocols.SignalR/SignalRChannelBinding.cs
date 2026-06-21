using ByteBard.AsyncAPI.Bindings;
using ByteBard.AsyncAPI.Models;
using ByteBard.AsyncAPI.Readers;
using ByteBard.AsyncAPI.Readers.ParseNodes;
using ByteBard.AsyncAPI.Writers;

namespace Bielu.AspNetCore.AsyncApi.Extensions.Protocols.SignalR;

/// <summary>
/// Channel binding for the <c>signalr</c> protocol. A channel maps to a single SignalR hub.
/// </summary>
public class SignalRChannelBinding : ChannelBinding<SignalRChannelBinding>
{
    /// <summary>The hub path/route, for example <c>/chatHub</c>.</summary>
    public string? Hub { get; set; }

    /// <summary>Transports the hub allows clients to negotiate (see <see cref="SignalRProtocol.Transports"/>).</summary>
    public IList<string> Transports { get; set; } = new List<string>();

    /// <summary>Hub protocols the hub supports (see <see cref="SignalRProtocol.HubProtocols"/>).</summary>
    public IList<string> Protocols { get; set; } = new List<string>();

    /// <summary>Schema describing the connection query string (for example an <c>access_token</c>).</summary>
    public AsyncApiJsonSchema? Query { get; set; }

    /// <summary>Schema describing the headers supplied during the negotiate request.</summary>
    public AsyncApiJsonSchema? Headers { get; set; }

    /// <inheritdoc />
    public override string BindingKey => SignalRProtocol.ProtocolName;

    /// <inheritdoc />
    protected override FixedFieldMap<SignalRChannelBinding> FixedFieldMap => new()
    {
        { "bindingVersion", (a, n) => a.BindingVersion = n.GetScalarValue() },
        { "hub", (a, n) => a.Hub = n.GetScalarValue() },
        { "transports", (a, n) => a.Transports = n.CreateSimpleList(s => s.GetScalarValue()) },
        { "protocols", (a, n) => a.Protocols = n.CreateSimpleList(s => s.GetScalarValue()) },
        { "query", (a, n) => a.Query = AsyncApiJsonSchemaDeserializer.LoadSchema(n) },
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
        writer.WriteOptionalProperty("hub", Hub);
        writer.WriteOptionalCollection("transports", Transports, (w, v) => w.WriteValue(v));
        writer.WriteOptionalCollection("protocols", Protocols, (w, v) => w.WriteValue(v));
        writer.WriteOptionalObject("query", Query, (w, s) => SignalRBindingSerializer.WriteSchema(w, s!, useV2Schema));
        writer.WriteOptionalObject("headers", Headers, (w, s) => SignalRBindingSerializer.WriteSchema(w, s!, useV2Schema));
        writer.WriteOptionalProperty("bindingVersion", BindingVersion ?? SignalRProtocol.DefaultBindingVersion);
        writer.WriteExtensions(Extensions);
        writer.WriteEndObject();
    }
}
