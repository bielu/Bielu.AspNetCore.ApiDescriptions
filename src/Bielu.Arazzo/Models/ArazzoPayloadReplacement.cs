using Bielu.Arazzo.Writers;

namespace Bielu.Arazzo.Models;

/// <summary>Spec §5.8.15 Payload Replacement Object.</summary>
public sealed class ArazzoPayloadReplacement : IArazzoSerializable, IArazzoExtensible
{
    /// <summary>A JSON Pointer, XPath expression, or JSONPath resolved against the request body.</summary>
    public required string Target { get; set; }

    /// <summary>Selector engine for <see cref="Target"/>. Defaults to jsonpointer for a "/"-prefixed target, jsonpath otherwise, per spec text following the fixed-fields table.</summary>
    public ArazzoSelectorType? TargetSelectorType { get; set; }

    public required ArazzoValue Value { get; set; }

    public IDictionary<string, System.Text.Json.Nodes.JsonNode?>? Extensions { get; set; }

    public void SerializeAsV1(IArazzoWriter writer)
    {
        writer.WriteStartObject();
        writer.WritePropertyName("target");
        writer.WriteValue(Target);
        if (TargetSelectorType is not null)
        {
            writer.WritePropertyName("targetSelectorType");
            TargetSelectorType.SerializeAsV1(writer);
        }

        writer.WritePropertyName("value");
        Value.SerializeAsV1(writer);
        writer.WriteExtensions(Extensions);
        writer.WriteEndObject();
    }
}
