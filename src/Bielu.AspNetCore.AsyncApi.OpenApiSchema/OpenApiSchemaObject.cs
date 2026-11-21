using System.Text.Json.Nodes;
using ByteBard.AsyncAPI.Models;
using ByteBard.AsyncAPI.Models.Interfaces;
using ByteBard.AsyncAPI.Writers;

namespace Bielu.AspNetCore.AsyncApi.OpenApiSchema;

/// <summary>
/// A payload schema in the OpenAPI 3.0 dialect, held as JSON. ByteBard's <see cref="AsyncApiJsonSchema"/> cannot hold
/// it: it writes <c>exclusiveMinimum</c> as a number and <c>discriminator</c> as a string, where OpenAPI 3.0 wants a
/// boolean and an object.
/// </summary>
/// <param name="schema">The OpenAPI Schema Object.</param>
/// <param name="v2References">
/// The references to OpenAPI component copies, each with the reference an AsyncAPI 2 document writes in its place.
/// </param>
/// <param name="v2Schema">
/// The JSON Schema an AsyncAPI 2 document writes instead, for a component schema: AsyncAPI 2 component schemas cannot
/// declare a schema format, so they stay JSON Schema.
/// </param>
internal sealed class OpenApiSchemaObject(
    JsonObject schema,
    IReadOnlyDictionary<string, string> v2References,
    AsyncApiJsonSchema? v2Schema = null) : IAsyncApiSchema
{
    public JsonObject Schema { get; } = schema;

    public void SerializeV2(IAsyncApiWriter writer)
    {
        if (v2Schema is not null)
        {
            v2Schema.SerializeV2(writer);
            return;
        }

        var written = Schema;
        if (v2References.Count > 0)
        {
            written = (JsonObject)Schema.DeepClone();
            RewriteReferences(written);
        }

        new AsyncApiAny(written).Write(writer);
    }

    public void SerializeV3(IAsyncApiWriter writer) => new AsyncApiAny(Schema).Write(writer);

    private void RewriteReferences(JsonNode? node)
    {
        switch (node)
        {
            case JsonObject value:
                if (value["$ref"]?.GetValue<string>() is { } reference
                    && v2References.TryGetValue(reference, out var v2Reference))
                {
                    value["$ref"] = v2Reference;
                }

                foreach (var (_, child) in value)
                {
                    RewriteReferences(child);
                }

                break;
            case JsonArray values:
                foreach (var child in values)
                {
                    RewriteReferences(child);
                }

                break;
        }
    }
}
