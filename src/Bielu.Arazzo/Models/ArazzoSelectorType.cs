using Bielu.Arazzo.Writers;

namespace Bielu.Arazzo.Models;

/// <summary>
/// The shape shared by Criterion.type, Selector.type, and PayloadReplacement.targetSelectorType:
/// a bare string (<c>"jsonpath"</c>, <c>"xpath"</c>, <c>"jsonpointer"</c>) or, when a non-default
/// engine version is required, the full Expression Type Object (spec §5.8.12).
/// </summary>
public sealed class ArazzoSelectorType : IArazzoSerializable
{
    /// <summary>One of "simple" (Criterion only, the implicit default), "regex" (Criterion only), "jsonpath", "xpath", or "jsonpointer".</summary>
    public required string Type { get; set; }

    /// <summary>
    /// Present only when the bare string form is insufficient. jsonpath: "rfc9535" (default) or
    /// "draft-goessner-dispatch-jsonpath-00". xpath: "xpath-31" (default), "xpath-30", "xpath-20", or
    /// "xpath-10". jsonpointer: "rfc6901".
    /// </summary>
    public string? Version { get; set; }

    public bool IsExpressionTypeObject => Version is not null;

    public static ArazzoSelectorType Simple { get; } = new() { Type = "simple" };

    public static implicit operator ArazzoSelectorType(string type) => new() { Type = type };

    public void SerializeAsV1(IArazzoWriter writer)
    {
        if (!IsExpressionTypeObject)
        {
            writer.WriteValue(Type);
            return;
        }

        writer.WriteStartObject();
        writer.WritePropertyName("type");
        writer.WriteValue(Type);
        writer.WritePropertyName("version");
        writer.WriteValue(Version);
        writer.WriteEndObject();
    }
}
