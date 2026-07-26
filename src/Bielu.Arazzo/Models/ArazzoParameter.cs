using Bielu.Arazzo.Writers;

namespace Bielu.Arazzo.Models;

/// <summary>
/// Spec §5.8.6 Parameter Object. <see cref="In"/> is required except when the containing step/action
/// targets a <c>workflowId</c>, in which case all parameters map to workflow inputs and "in" is omitted.
/// </summary>
public sealed class ArazzoParameter : IArazzoSerializable, IArazzoExtensible
{
    public required string Name { get; set; }

    /// <summary>One of "path", "query", "querystring", "header", or "cookie".</summary>
    public string? In { get; set; }

    public required ArazzoValue Value { get; set; }

    public IDictionary<string, System.Text.Json.Nodes.JsonNode?>? Extensions { get; set; }

    public void SerializeAsV1(IArazzoWriter writer)
    {
        writer.WriteStartObject();
        writer.WritePropertyName("name");
        writer.WriteValue(Name);
        writer.WriteOptionalProperty("in", In);
        writer.WritePropertyName("value");
        Value.SerializeAsV1(writer);
        writer.WriteExtensions(Extensions);
        writer.WriteEndObject();
    }
}

public static class ArazzoParameterLocation
{
    public const string Path = "path";
    public const string Query = "query";
    public const string QueryString = "querystring";
    public const string Header = "header";
    public const string Cookie = "cookie";
}
