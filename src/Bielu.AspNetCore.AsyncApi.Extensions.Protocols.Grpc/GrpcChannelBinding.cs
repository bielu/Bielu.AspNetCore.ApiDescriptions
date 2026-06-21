using ByteBard.AsyncAPI.Bindings;
using ByteBard.AsyncAPI.Readers.ParseNodes;
using ByteBard.AsyncAPI.Writers;

namespace Bielu.AspNetCore.AsyncApi.Extensions.Protocols.Grpc;

/// <summary>
/// Channel binding for the <c>grpc</c> protocol. A channel maps to a single gRPC service (a proto
/// <c>service</c> declaration).
/// </summary>
public class GrpcChannelBinding : ChannelBinding<GrpcChannelBinding>
{
    /// <summary>The fully-qualified service name, for example <c>greet.Greeter</c>.</summary>
    public string? Service { get; set; }

    /// <summary>The protobuf package the service belongs to, for example <c>greet</c>.</summary>
    public string? Package { get; set; }

    /// <summary>Path or URL to the <c>.proto</c> file that defines the service.</summary>
    public string? ProtoFile { get; set; }

    /// <inheritdoc />
    public override string BindingKey => GrpcProtocol.ProtocolName;

    /// <inheritdoc />
    protected override FixedFieldMap<GrpcChannelBinding> FixedFieldMap => new()
    {
        { "bindingVersion", (a, n) => a.BindingVersion = n.GetScalarValue() },
        { "service", (a, n) => a.Service = n.GetScalarValue() },
        { "package", (a, n) => a.Package = n.GetScalarValue() },
        { "protoFile", (a, n) => a.ProtoFile = n.GetScalarValue() },
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
        writer.WriteOptionalProperty("service", Service);
        writer.WriteOptionalProperty("package", Package);
        writer.WriteOptionalProperty("protoFile", ProtoFile);
        writer.WriteOptionalProperty("bindingVersion", BindingVersion ?? GrpcProtocol.DefaultBindingVersion);
        writer.WriteExtensions(Extensions);
        writer.WriteEndObject();
    }
}
