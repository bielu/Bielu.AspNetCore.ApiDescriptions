using Bielu.Arazzo.Writers;

namespace Bielu.Arazzo.Models;

/// <summary>
/// Spec §5.8.11 Criterion Object, used by Step.successCriteria and the criteria arrays on success/failure
/// actions. Four condition flavors: simple (default), regex, jsonpath, xpath.
/// </summary>
public sealed class ArazzoCriterion : IArazzoSerializable, IArazzoExtensible
{
    /// <summary>Runtime expression setting the context the condition applies to. Required whenever <see cref="Type"/> is not simple.</summary>
    public string? Context { get; set; }

    public required string Condition { get; set; }

    /// <summary>Defaults to "simple" when omitted.</summary>
    public ArazzoSelectorType? Type { get; set; }

    public IDictionary<string, System.Text.Json.Nodes.JsonNode?>? Extensions { get; set; }

    public void SerializeAsV1(IArazzoWriter writer)
    {
        writer.WriteStartObject();
        writer.WriteOptionalProperty("context", Context);
        writer.WritePropertyName("condition");
        writer.WriteValue(Condition);
        if (Type is not null)
        {
            writer.WritePropertyName("type");
            Type.SerializeAsV1(writer);
        }

        writer.WriteExtensions(Extensions);
        writer.WriteEndObject();
    }
}

public static class ArazzoCriterionType
{
    public const string Simple = "simple";
    public const string Regex = "regex";
    public const string JsonPath = "jsonpath";
    public const string XPath = "xpath";
}
