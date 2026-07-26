using System.Text.Json.Nodes;
using Bielu.Arazzo.Writers;

namespace Bielu.Arazzo.Models;

/// <summary>Spec §5.8.1 Arazzo Specification Object — the root of an Arazzo Description.</summary>
public sealed class ArazzoDocument : IArazzoSerializable, IArazzoExtensible
{
    /// <summary>The Arazzo Specification version this document uses, e.g. "1.1.0".</summary>
    public required string Arazzo { get; set; }

    /// <summary>Self-assigned URI for this document; also its base URI for resolving relative references. MUST NOT contain a fragment.</summary>
    public string? Self { get; set; }

    public required ArazzoInfo Info { get; set; }

    public required IList<ArazzoSourceDescription> SourceDescriptions { get; set; }

    public required IList<ArazzoWorkflow> Workflows { get; set; }

    public ArazzoComponents? Components { get; set; }

    public IDictionary<string, JsonNode?>? Extensions { get; set; }

    public void SerializeAsV1(IArazzoWriter writer)
    {
        writer.WriteStartObject();
        writer.WritePropertyName("arazzo");
        writer.WriteValue(Arazzo);
        writer.WriteOptionalProperty("$self", Self);
        writer.WritePropertyName("info");
        Info.SerializeAsV1(writer);

        writer.WritePropertyName("sourceDescriptions");
        writer.WriteStartArray();
        foreach (var source in SourceDescriptions)
        {
            source.SerializeAsV1(writer);
        }

        writer.WriteEndArray();

        writer.WritePropertyName("workflows");
        writer.WriteStartArray();
        foreach (var workflow in Workflows)
        {
            workflow.SerializeAsV1(writer);
        }

        writer.WriteEndArray();

        if (Components is not null)
        {
            writer.WritePropertyName("components");
            Components.SerializeAsV1(writer);
        }

        writer.WriteExtensions(Extensions);
        writer.WriteEndObject();
    }
}
