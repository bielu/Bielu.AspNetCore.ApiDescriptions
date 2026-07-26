using Bielu.Arazzo.Writers;

namespace Bielu.Arazzo.Models;

/// <summary>
/// Spec §5.8.3 Source Description Object. <see cref="Type"/> allowing <c>"asyncapi"</c> is new in 1.1.0 —
/// the change that makes Arazzo relevant to this suite (see ARAZZO-PROPOSAL.md §1).
/// </summary>
public sealed class ArazzoSourceDescription : IArazzoSerializable, IArazzoExtensible
{
    public required string Name { get; set; }

    public required string Url { get; set; }

    /// <summary>One of "openapi", "asyncapi", or "arazzo".</summary>
    public string? Type { get; set; }

    public IDictionary<string, System.Text.Json.Nodes.JsonNode?>? Extensions { get; set; }

    public void SerializeAsV1(IArazzoWriter writer)
    {
        writer.WriteStartObject();
        writer.WritePropertyName("name");
        writer.WriteValue(Name);
        writer.WritePropertyName("url");
        writer.WriteValue(Url);
        writer.WriteOptionalProperty("type", Type);
        writer.WriteExtensions(Extensions);
        writer.WriteEndObject();
    }
}

public static class ArazzoSourceDescriptionType
{
    public const string OpenApi = "openapi";
    public const string AsyncApi = "asyncapi";
    public const string Arazzo = "arazzo";
}
