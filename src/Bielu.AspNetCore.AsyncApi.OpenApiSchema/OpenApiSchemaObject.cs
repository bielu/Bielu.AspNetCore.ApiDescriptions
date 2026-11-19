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
internal sealed class OpenApiSchemaObject(JsonObject schema) : IAsyncApiSchema
{
    public JsonObject Schema { get; } = schema;

    public void SerializeV2(IAsyncApiWriter writer) => new AsyncApiAny(Schema).Write(writer);

    public void SerializeV3(IAsyncApiWriter writer) => new AsyncApiAny(Schema).Write(writer);
}
