using Bielu.AspNetCore.AsyncApi.Services;

namespace Bielu.AspNetCore.AsyncApi.Protobuf;

/// <summary>Describes an AsyncAPI document's Protocol Buffers payloads as <c>.proto</c> schemas.</summary>
public static class AsyncApiProtobufOptionsExtensions
{
    /// <summary>
    /// Replaces the payload of every message whose type is a Protocol Buffers message (an <c>IMessage</c>, as
    /// <c>Grpc.Tools</c> generates) with its <c>.proto</c> source, declared as
    /// <c>schemaFormat: application/vnd.google.protobuf;version=3</c> (or <c>2</c> for a proto2 file).
    /// </summary>
    /// <remarks>
    /// <para>
    /// The source is rendered from the message's descriptor, so field numbers, types, labels, oneofs, maps, nested
    /// types, enums and reserved ranges are those of the generated code, which is the real wire contract. It is a
    /// single file holding the message, marked <c>@RootNode</c>, and every message and enum it uses; the well-known
    /// <c>google/protobuf</c> and <c>google/type</c> types stay imports.
    /// </para>
    /// <para>
    /// Payloads of other types keep their JSON Schema, and the JSON Schemas stay under <c>components/schemas</c>. Add this
    /// after anything that adds messages, such as <c>AddWolverine()</c>, since its document transformer runs where it is added.
    /// </para>
    /// </remarks>
    /// <param name="options">The options of the document to describe.</param>
    /// <param name="configure">Configures the <see cref="ProtobufPayloadOptions"/>.</param>
    /// <returns>The <see cref="AsyncApiOptions"/> instance for further customization.</returns>
    /// <example>
    /// <code>
    /// builder.Services.AddAsyncApi(options => options
    ///     .AddServer("grpc", "localhost:5001", "grpc")
    ///     .AddProtobufPayloads());
    /// </code>
    /// </example>
    public static AsyncApiOptions AddProtobufPayloads(this AsyncApiOptions options, Action<ProtobufPayloadOptions>? configure = null)
    {
        ArgumentNullException.ThrowIfNull(options);

        var protobufOptions = new ProtobufPayloadOptions();
        configure?.Invoke(protobufOptions);

        var transformer = new ProtobufPayloadTransformer(protobufOptions);
        return options
            .AddSchemaTransformer(transformer)
            .AddDocumentTransformer(transformer);
    }
}
