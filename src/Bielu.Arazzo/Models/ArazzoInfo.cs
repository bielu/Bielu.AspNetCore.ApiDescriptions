using Bielu.Arazzo.Writers;

namespace Bielu.Arazzo.Models;

/// <summary>Spec §5.8.2 Info Object.</summary>
public sealed class ArazzoInfo : IArazzoSerializable, IArazzoExtensible
{
    public required string Title { get; set; }

    public string? Summary { get; set; }

    public string? Description { get; set; }

    /// <summary>The Arazzo document's own version, distinct from the Arazzo Specification version.</summary>
    public required string Version { get; set; }

    public IDictionary<string, System.Text.Json.Nodes.JsonNode?>? Extensions { get; set; }

    public void SerializeAsV1(IArazzoWriter writer)
    {
        writer.WriteStartObject();
        writer.WritePropertyName("title");
        writer.WriteValue(Title);
        writer.WriteOptionalProperty("summary", Summary);
        writer.WriteOptionalProperty("description", Description);
        writer.WritePropertyName("version");
        writer.WriteValue(Version);
        writer.WriteExtensions(Extensions);
        writer.WriteEndObject();
    }
}
