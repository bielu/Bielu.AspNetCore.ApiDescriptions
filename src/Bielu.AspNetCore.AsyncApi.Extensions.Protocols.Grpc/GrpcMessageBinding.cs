using ByteBard.AsyncAPI.Bindings;
using ByteBard.AsyncAPI.Models;
using ByteBard.AsyncAPI.Readers;
using ByteBard.AsyncAPI.Readers.ParseNodes;
using ByteBard.AsyncAPI.Writers;

namespace Bielu.AspNetCore.AsyncApi.Extensions.Protocols.Grpc;

/// <summary>
/// Message binding for the <c>grpc</c> protocol. Describes how a message is framed on the wire as a
/// protobuf (or gRPC-JSON transcoded) payload.
/// </summary>
public class GrpcMessageBinding : MessageBinding<GrpcMessageBinding>
{
    /// <summary>The fully-qualified protobuf message type, for example <c>greet.HelloRequest</c>.</summary>
    public string? MessageType { get; set; }

    /// <summary>How the payload is encoded (see <see cref="GrpcProtocol.MessageEncodings"/>).</summary>
    public string? Encoding { get; set; }

    /// <summary>Schema describing optional call metadata (gRPC headers/trailers) carried with the message.</summary>
    public AsyncApiJsonSchema? Headers { get; set; }

    /// <inheritdoc />
    public override string BindingKey => GrpcProtocol.ProtocolName;

    /// <inheritdoc />
    protected override FixedFieldMap<GrpcMessageBinding> FixedFieldMap => new()
    {
        { "bindingVersion", (a, n) => a.BindingVersion = n.GetScalarValue() },
        { "messageType", (a, n) => a.MessageType = n.GetScalarValue() },
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
        writer.WriteOptionalProperty("messageType", MessageType);
        writer.WriteOptionalProperty("encoding", Encoding);
        writer.WriteOptionalObject("headers", Headers, (w, s) => GrpcBindingSerializer.WriteSchema(w, s!, useV2Schema));
        writer.WriteOptionalProperty("bindingVersion", BindingVersion ?? GrpcProtocol.DefaultBindingVersion);
        writer.WriteExtensions(Extensions);
        writer.WriteEndObject();
    }
}
