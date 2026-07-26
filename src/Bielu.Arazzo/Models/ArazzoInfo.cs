using Bielu.Arazzo.Writers;

namespace Bielu.Arazzo.Models;

/// <summary>Spec §5.8.2 Info Object.</summary>
public sealed class ArazzoInfo : IArazzoSerializable, IArazzoExtensible
{
    /// <summary>The title of the Arazzo Description.</summary>
    public required string Title { get; set; }

    /// <summary>A short summary of the Arazzo Description.</summary>
    public string? Summary { get; set; }

    /// <summary>A verbose description of the Arazzo Description.</summary>
    public string? Description { get; set; }

    /// <summary>The Arazzo document's own version, distinct from the Arazzo Specification version.</summary>
    public required string Version { get; set; }

    /// <summary>A dictionary of extension properties.</summary>
    public IDictionary<string, System.Text.Json.Nodes.JsonNode?>? Extensions { get; set; }

    /// <summary>Writes this model's Arazzo 1.x representation via the given writer.</summary>
    public void SerializeAsV1(IArazzoWriter writer)
    {
        ArgumentNullException.ThrowIfNull(writer);
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
