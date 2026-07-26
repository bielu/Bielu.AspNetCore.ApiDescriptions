using Bielu.Arazzo.Writers;

namespace Bielu.Arazzo.Models;

/// <summary>Spec §5.8.14 Request Body Object.</summary>
public sealed class ArazzoRequestBody : IArazzoSerializable, IArazzoExtensible
{
    public string? ContentType { get; set; }

    public ArazzoValue? Payload { get; set; }

    public IList<ArazzoPayloadReplacement>? Replacements { get; set; }

    public IDictionary<string, System.Text.Json.Nodes.JsonNode?>? Extensions { get; set; }

    public void SerializeAsV1(IArazzoWriter writer)
    {
        writer.WriteStartObject();
        writer.WriteOptionalProperty("contentType", ContentType);
        if (Payload is not null)
        {
            writer.WritePropertyName("payload");
            Payload.SerializeAsV1(writer);
        }

        writer.WriteOptionalArrayProperty("replacements", Replacements, r => r.SerializeAsV1(writer));
        writer.WriteExtensions(Extensions);
        writer.WriteEndObject();
    }
}
