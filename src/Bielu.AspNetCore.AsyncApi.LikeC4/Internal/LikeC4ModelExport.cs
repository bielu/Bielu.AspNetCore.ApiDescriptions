using System.Text.Json;
using System.Text.Json.Nodes;

namespace Bielu.AspNetCore.AsyncApi.LikeC4.Internal;

/// <summary>
/// The elements and relationships of a LikeC4 model, read from the JSON that <c>likec4 export json</c> writes.
/// </summary>
internal sealed record LikeC4ModelExport(IReadOnlyList<LikeC4ExportElement> Elements, IReadOnlyList<LikeC4ExportRelation> Relations)
{
    /// <exception cref="FormatException">The content is not a LikeC4 JSON export.</exception>
    public static LikeC4ModelExport Read(string content)
    {
        JsonNode? root;
        try
        {
            root = JsonNode.Parse(content);
        }
        catch (JsonException ex)
        {
            throw new FormatException($"The LikeC4 model is not JSON: {ex.Message}", ex);
        }

        if (root is not JsonObject model || model["elements"] is not JsonObject elements)
        {
            throw new FormatException("The LikeC4 model has no 'elements': write it with 'likec4 export json'.");
        }

        var elementList = new List<LikeC4ExportElement>();
        foreach (var (id, node) in elements)
        {
            var metadata = new Dictionary<string, string>(StringComparer.Ordinal);
            foreach (var (key, value) in node?["metadata"] as JsonObject ?? [])
            {
                // A metadata value is a string, or a list of them.
                if ((value is JsonArray list ? list.FirstOrDefault() : value) is { } first
                    && AsyncApiDocumentReader.Text(first) is { } text)
                {
                    metadata[key] = text;
                }
            }

            elementList.Add(new LikeC4ExportElement(id, AsyncApiDocumentReader.Text(node?["title"]) ?? id, metadata));
        }

        var relationList = new List<LikeC4ExportRelation>();
        foreach (var (id, node) in model["relations"] as JsonObject ?? [])
        {
            if (Endpoint(node?["source"]) is { } source && Endpoint(node?["target"]) is { } target)
            {
                relationList.Add(new LikeC4ExportRelation(id, source, target, AsyncApiDocumentReader.Text(node?["title"])));
            }
        }

        return new LikeC4ModelExport(elementList, relationList);
    }

    /// <summary>A relationship's end: <c>{ "model": "&lt;id&gt;" }</c>, or the id itself in older exports.</summary>
    private static string? Endpoint(JsonNode? node) =>
        AsyncApiDocumentReader.Text(node is JsonObject endpoint ? endpoint["model"] : node);
}

/// <summary>An element of an exported model: its full id (<c>cloud.orders</c>), title and metadata.</summary>
internal sealed record LikeC4ExportElement(string Id, string Title, IReadOnlyDictionary<string, string> Metadata);

/// <summary>A relationship of an exported model, between two elements' full ids.</summary>
internal sealed record LikeC4ExportRelation(string Id, string Source, string Target, string? Title);
