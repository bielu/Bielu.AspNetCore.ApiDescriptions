using ByteBard.AsyncAPI.Bindings;
using ByteBard.AsyncAPI.Readers.ParseNodes;
using ByteBard.AsyncAPI.Writers;

namespace Bielu.AspNetCore.AsyncApi.Extensions.Protocols.SignalR;

/// <summary>
/// Server binding for the <c>signalr</c> protocol. Describes capabilities advertised by a SignalR
/// endpoint such as the transports and hub protocols it supports.
/// </summary>
public class SignalRServerBinding : ServerBinding<SignalRServerBinding>
{
    /// <summary>Transports the server supports (see <see cref="SignalRProtocol.Transports"/>).</summary>
    public IList<string> Transports { get; set; } = new List<string>();

    /// <summary>Hub protocols the server supports (see <see cref="SignalRProtocol.HubProtocols"/>).</summary>
    public IList<string> Protocols { get; set; } = new List<string>();

    /// <inheritdoc />
    public override string BindingKey => SignalRProtocol.ProtocolName;

    /// <inheritdoc />
    protected override FixedFieldMap<SignalRServerBinding> FixedFieldMap => new()
    {
        { "bindingVersion", (a, n) => a.BindingVersion = n.GetScalarValue() },
        { "transports", (a, n) => a.Transports = n.CreateSimpleList(s => s.GetScalarValue()) },
        { "protocols", (a, n) => a.Protocols = n.CreateSimpleList(s => s.GetScalarValue()) },
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
        writer.WriteOptionalCollection("transports", Transports, (w, v) => w.WriteValue(v));
        writer.WriteOptionalCollection("protocols", Protocols, (w, v) => w.WriteValue(v));
        writer.WriteOptionalProperty("bindingVersion", BindingVersion ?? SignalRProtocol.DefaultBindingVersion);
        writer.WriteExtensions(Extensions);
        writer.WriteEndObject();
    }
}
