using System.Text.Json.Nodes;

namespace Bielu.Arazzo.Models;

/// <summary>Implemented by every model object the spec marks as extendable with Specification Extensions (<c>x-*</c>).</summary>
public interface IArazzoExtensible
{
    IDictionary<string, JsonNode?>? Extensions { get; set; }
}

public static class ArazzoExtensibleWriterExtensions
{
    public static void WriteExtensions(this Writers.IArazzoWriter writer, IDictionary<string, JsonNode?>? extensions)
    {
        if (extensions is null)
        {
            return;
        }

        foreach (var (key, value) in extensions)
        {
            writer.WritePropertyName(key);
            writer.WriteRaw(value);
        }
    }
}
