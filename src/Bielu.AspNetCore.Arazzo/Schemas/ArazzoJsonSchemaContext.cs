using System.Text.Json.Nodes;
using System.Text.Json.Serialization;

namespace Bielu.AspNetCore.Arazzo.Schemas;

[JsonSerializable(typeof(string))]
[JsonSerializable(typeof(JsonNode))]
[JsonSerializable(typeof(JsonObject))]
[JsonSerializable(typeof(JsonArray))]
[JsonSerializable(typeof(ArazzoProblemDetails))]
internal sealed partial class ArazzoJsonSchemaContext : JsonSerializerContext;

internal sealed record ArazzoProblemDetails(
    [property: JsonPropertyName("type")] string Type,
    [property: JsonPropertyName("title")] string Title,
    [property: JsonPropertyName("status")] int Status,
    [property: JsonPropertyName("detail")] string Detail);
