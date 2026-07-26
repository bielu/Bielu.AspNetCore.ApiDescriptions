using System.Text.Json.Nodes;

namespace Bielu.Arazzo.Models;

/// <summary>Implemented by every model object the spec marks as extendable with Specification Extensions (<c>x-*</c>).</summary>
public interface IArazzoExtensible
{
    /// <summary>Specification Extension properties. Keys must use the required <c>x-</c> prefix.</summary>
    IDictionary<string, JsonNode?>? Extensions { get; set; }
}

public static class ArazzoExtensibleWriterExtensions
{
    /// <summary>
    /// Writes each entry in <paramref name="extensions"/> after validating that every key has the
    /// <c>x-</c> prefix required by Specification Extensions.
    /// </summary>
    public static void WriteExtensions(this Writers.IArazzoWriter writer, IDictionary<string, JsonNode?>? extensions)
    {
        ArgumentNullException.ThrowIfNull(writer);

        if (extensions is null)
        {
            return;
        }

        foreach (var (key, value) in extensions)
        {
            if (!key.StartsWith("x-", StringComparison.Ordinal))
            {
                throw new ArgumentException($"Extension key '{key}' must start with 'x-'.", nameof(extensions));
            }

            writer.WritePropertyName(key);
            writer.WriteRaw(value);
        }
    }
}
