using ByteBard.AsyncAPI.Models;
using ByteBard.AsyncAPI.Writers;

namespace Bielu.AspNetCore.AsyncApi.Avro;

/// <summary>An Avro logical type: a primitive annotated with a <c>logicalType</c>, such as <c>uuid</c>.</summary>
/// <remarks>
/// ByteBard's own logical types (<c>AvroUUID</c>, <c>AvroTimestampMillis</c>, …) write their <c>logicalType</c> only
/// in AsyncAPI 2.x documents: in 3.x they fall back to the bare primitive. This one writes the same object for both.
/// </remarks>
/// <param name="type">The underlying primitive, for example <c>string</c>.</param>
/// <param name="logicalType">The logical type, for example <c>uuid</c>.</param>
internal sealed class AvroLogicalTypeSchema(string type, string logicalType) : AsyncApiAvroSchema
{
    public override string Type { get; } = type;

    public string LogicalType { get; } = logicalType;

    public override IDictionary<string, AsyncApiAny> Metadata { get; set; } = new Dictionary<string, AsyncApiAny>();

    public override void SerializeV2(IAsyncApiWriter writer) => Serialize(writer);

    public override void SerializeV3(IAsyncApiWriter writer) => Serialize(writer);

    private void Serialize(IAsyncApiWriter writer)
    {
        writer.WriteStartObject();
        writer.WriteOptionalProperty("type", Type);
        writer.WriteOptionalProperty("logicalType", LogicalType);
        writer.WriteEndObject();
    }
}
