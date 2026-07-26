using System.Text.Json;
using System.Text.Json.Nodes;
using Json.Pointer;

namespace Bielu.Arazzo.Expressions;

internal static class ArazzoJsonPointerHelper
{
    /// <summary>Evaluates an RFC 6901 pointer against <paramref name="root"/>; an absent or empty pointer means the whole value.</summary>
    public static JsonNode? Evaluate(string? pointer, JsonNode? root)
    {
        if (string.IsNullOrEmpty(pointer) || root is null)
        {
            return root;
        }

        try
        {
            // JsonNode -> JsonElement via text round-trip rather than JsonSerializer.Deserialize<JsonElement>,
            // to avoid a reflection-based serializer call in an IsAotCompatible project.
            using var document = JsonDocument.Parse(root.ToJsonString());
            var result = JsonPointer.Parse(pointer).Evaluate(document.RootElement);
            return result is null ? null : JsonNode.Parse(result.Value.GetRawText());
        }
        catch (Exception)
        {
            // Malformed pointer syntax: spec §5.8.11.4.5 requires evaluation errors to fail, not throw.
            return null;
        }
    }
}
