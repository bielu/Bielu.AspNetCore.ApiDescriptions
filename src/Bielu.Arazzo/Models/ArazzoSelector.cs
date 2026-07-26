using Bielu.Arazzo.Writers;

namespace Bielu.Arazzo.Models;

/// <summary>Spec §5.8.13 Selector Object: fine-grained data selection from structured data via JSONPath, XPath, or JSON Pointer.</summary>
public sealed class ArazzoSelector : IArazzoSerializable, IArazzoExtensible
{
    /// <summary>Runtime expression that MUST evaluate to structured data (e.g. <c>$response.body</c>).</summary>
    public required string Context { get; set; }

    /// <summary>The selector expression itself, e.g. <c>$.items[0].id</c> or <c>/Envelope/Item</c>.</summary>
    public required string Selector { get; set; }

    public required ArazzoSelectorType Type { get; set; }

    public IDictionary<string, System.Text.Json.Nodes.JsonNode?>? Extensions { get; set; }

    public void SerializeAsV1(IArazzoWriter writer)
    {
        ArgumentNullException.ThrowIfNull(writer);
        writer.WriteStartObject();
        writer.WritePropertyName("context");
        writer.WriteValue(Context);
        writer.WritePropertyName("selector");
        writer.WriteValue(Selector);
        writer.WritePropertyName("type");
        Type.SerializeAsV1(writer);
        writer.WriteExtensions(Extensions);
        writer.WriteEndObject();
    }
}
