using ByteBard.AsyncAPI.Models.Interfaces;
using ByteBard.AsyncAPI.Writers;

namespace Bielu.AspNetCore.AsyncApi.Protobuf;

/// <summary>A payload schema in the Protobuf format: the <c>.proto</c> source, written as a string.</summary>
/// <param name="source">The <c>.proto</c> source.</param>
internal sealed class ProtobufSchema(string source) : IAsyncApiSchema
{
    public string Source { get; } = source;

    public void SerializeV2(IAsyncApiWriter writer) => writer.WriteValue(Source);

    public void SerializeV3(IAsyncApiWriter writer) => writer.WriteValue(Source);
}
