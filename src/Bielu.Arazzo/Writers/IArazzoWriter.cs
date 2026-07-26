using System.Text.Json.Nodes;

namespace Bielu.Arazzo.Writers;

/// <summary>
/// Format-agnostic primitives that <see cref="Bielu.Arazzo.Models.IArazzoSerializable"/> implementations
/// call against. <see cref="ArazzoJsonWriter"/> and <see cref="ArazzoYamlWriter"/> both implement this
/// so every model type has a single serialization path regardless of output format.
/// </summary>
public interface IArazzoWriter
{
    /// <summary>Writes the start of an object.</summary>
    void WriteStartObject();

    /// <summary>Writes the end of an object.</summary>
    void WriteEndObject();

    /// <summary>Writes the start of an array.</summary>
    void WriteStartArray();

    /// <summary>Writes the end of an array.</summary>
    void WriteEndArray();

    /// <summary>Writes a property name.</summary>
    void WritePropertyName(string name);

    /// <summary>Writes a string value.</summary>
    void WriteValue(string? value);

    /// <summary>Writes a double value.</summary>
    void WriteValue(double value);

    /// <summary>Writes a boolean value.</summary>
    void WriteValue(bool value);

    /// <summary>Writes an integer value.</summary>
    void WriteValue(int value);

    /// <summary>Writes a null value.</summary>
    void WriteNull();

    /// <summary>Writes an already-parsed JSON value (e.g. an inline JSON Schema or an extension value) verbatim.</summary>
    void WriteRaw(JsonNode? node);

    /// <summary>Writes an optional string property.</summary>
    void WriteOptionalProperty(string name, string? value)
    {
        ArgumentNullException.ThrowIfNull(name);
        if (value is null)
        {
            return;
        }

        WritePropertyName(name);
        WriteValue(value);
    }

    /// <summary>Writes an optional integer property.</summary>
    void WriteOptionalProperty(string name, int? value)
    {
        ArgumentNullException.ThrowIfNull(name);
        if (value is null)
        {
            return;
        }

        WritePropertyName(name);
        WriteValue(value.Value);
    }

    /// <summary>Writes an optional double property.</summary>
    void WriteOptionalProperty(string name, double? value)
    {
        ArgumentNullException.ThrowIfNull(name);
        if (value is null)
        {
            return;
        }

        WritePropertyName(name);
        WriteValue(value.Value);
    }

    /// <summary>Writes an optional boolean property.</summary>
    void WriteOptionalProperty(string name, bool? value)
    {
        ArgumentNullException.ThrowIfNull(name);
        if (value is null)
        {
            return;
        }

        WritePropertyName(name);
        WriteValue(value.Value);
    }

    /// <summary>Writes an optional JSON node property.</summary>
    void WriteOptionalProperty(string name, JsonNode? value)
    {
        ArgumentNullException.ThrowIfNull(name);
        if (value is null)
        {
            return;
        }

        WritePropertyName(name);
        WriteRaw(value);
    }

    /// <summary>Writes an optional array property.</summary>
    void WriteOptionalArrayProperty<T>(string name, ICollection<T>? items, Action<T> writeItem)
    {
        ArgumentNullException.ThrowIfNull(name);
        ArgumentNullException.ThrowIfNull(writeItem);
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

    /// <summary>Writes an optional map property.</summary>
    void WriteOptionalMapProperty<T>(string name, IDictionary<string, T>? map, Action<T> writeValue)
    {
        ArgumentNullException.ThrowIfNull(name);
        ArgumentNullException.ThrowIfNull(writeValue);
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
