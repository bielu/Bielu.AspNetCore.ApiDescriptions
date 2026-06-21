using ByteBard.AsyncAPI.Bindings;
using ByteBard.AsyncAPI.Readers.ParseNodes;
using ByteBard.AsyncAPI.Writers;

namespace Bielu.AspNetCore.AsyncApi.Extensions.Protocols.Grpc;

/// <summary>
/// Server binding for the <c>grpc</c> protocol. Describes capabilities advertised by a gRPC
/// endpoint such as the services it hosts and whether server reflection and TLS are enabled.
/// </summary>
public class GrpcServerBinding : ServerBinding<GrpcServerBinding>
{
    /// <summary>Fully-qualified names of the services hosted by the server (for example <c>greet.Greeter</c>).</summary>
    public IList<string> Services { get; set; } = new List<string>();

    /// <summary>Whether the server exposes the gRPC server reflection service.</summary>
    public bool? Reflection { get; set; }

    /// <summary>Whether the endpoint requires TLS.</summary>
    public bool? Tls { get; set; }

    /// <summary>Compression algorithms the server supports (see <see cref="GrpcProtocol.Compressions"/>).</summary>
    public IList<string> Compressions { get; set; } = new List<string>();

    /// <inheritdoc />
    public override string BindingKey => GrpcProtocol.ProtocolName;

    /// <inheritdoc />
    protected override FixedFieldMap<GrpcServerBinding> FixedFieldMap => new()
    {
        { "bindingVersion", (a, n) => a.BindingVersion = n.GetScalarValue() },
        { "services", (a, n) => a.Services = n.CreateSimpleList(s => s.GetScalarValue()) },
        { "reflection", (a, n) => a.Reflection = n.GetBooleanValue() },
        { "tls", (a, n) => a.Tls = n.GetBooleanValue() },
        { "compressions", (a, n) => a.Compressions = n.CreateSimpleList(s => s.GetScalarValue()) },
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
        writer.WriteOptionalCollection("services", Services, (w, v) => w.WriteValue(v));
        writer.WriteOptionalProperty("reflection", Reflection);
        writer.WriteOptionalProperty("tls", Tls);
        writer.WriteOptionalCollection("compressions", Compressions, (w, v) => w.WriteValue(v));
        writer.WriteOptionalProperty("bindingVersion", BindingVersion ?? GrpcProtocol.DefaultBindingVersion);
        writer.WriteExtensions(Extensions);
        writer.WriteEndObject();
    }
}
