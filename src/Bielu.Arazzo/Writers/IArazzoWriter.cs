using System.Text.Json.Nodes;

namespace Bielu.Arazzo.Writers;

/// <summary>
/// Format-agnostic primitives that <see cref="Bielu.Arazzo.Models.IArazzoSerializable"/> implementations
/// call against. <see cref="ArazzoJsonWriter"/> and <see cref="ArazzoYamlWriter"/> both implement this
/// so every model type has a single serialization path regardless of output format.
/// </summary>
public interface IArazzoWriter
{
    void WriteStartObject();

    void WriteEndObject();

    void WriteStartArray();

    void WriteEndArray();

    void WritePropertyName(string name);

    void WriteValue(string? value);

    void WriteValue(double value);

    void WriteValue(bool value);

    void WriteValue(int value);

    void WriteNull();

    /// <summary>Writes an already-parsed JSON value (e.g. an inline JSON Schema or an extension value) verbatim.</summary>
    void WriteRaw(JsonNode? node);

    void WriteOptionalProperty(string name, string? value)
    {
        if (value is null)
        {
            return;
        }

        WritePropertyName(name);
        WriteValue(value);
    }

    void WriteOptionalProperty(string name, int? value)
    {
        if (value is null)
        {
            return;
        }

        WritePropertyName(name);
        WriteValue(value.Value);
    }

    void WriteOptionalProperty(string name, double? value)
    {
        if (value is null)
        {
            return;
        }

        WritePropertyName(name);
        WriteValue(value.Value);
    }

    void WriteOptionalProperty(string name, bool? value)
    {
        if (value is null)
        {
            return;
        }

        WritePropertyName(name);
        WriteValue(value.Value);
    }

    void WriteOptionalProperty(string name, JsonNode? value)
    {
        if (value is null)
        {
            return;
        }

        WritePropertyName(name);
        WriteRaw(value);
    }

    void WriteOptionalArrayProperty<T>(string name, ICollection<T>? items, Action<T> writeItem)
    {
        if (items is null || items.Count == 0)
        {
            return;
        }

        WritePropertyName(name);
        WriteStartArray();
        foreach (var item in items)
        {
            writeItem(item);
        }

        WriteEndArray();
    }

    void WriteOptionalMapProperty<T>(string name, IDictionary<string, T>? map, Action<T> writeValue)
    {
        if (map is null || map.Count == 0)
        {
            return;
        }

        WritePropertyName(name);
        WriteStartObject();
        foreach (var (key, value) in map)
        {
            WritePropertyName(key);
            writeValue(value);
        }

        WriteEndObject();
    }
}
