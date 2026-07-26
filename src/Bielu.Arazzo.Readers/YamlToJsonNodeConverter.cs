using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using YamlDotNet.Core;
using YamlDotNet.RepresentationModel;

namespace Bielu.Arazzo.Readers;

/// <summary>
/// Converts a parsed YAML document into the same <see cref="JsonNode"/> tree a JSON document would
/// produce, so <see cref="ArazzoV1Deserializer"/> has exactly one tree-walking implementation regardless
/// of source format — the intent behind ByteBard.AsyncAPI.NET's unified ParseNode abstraction, achieved
/// here by converting into the BCL's own node type instead of a bespoke one.
/// </summary>
internal static class YamlToJsonNodeConverter
{
    private static readonly Regex JsonNumberPattern = new(@"^-?(0|[1-9]\d*)(\.\d+)?([eE][+-]?\d+)?$", RegexOptions.Compiled);

    public static JsonNode? Convert(TextReader reader)
    {
        var yamlStream = new YamlStream();
        yamlStream.Load(reader);
        return yamlStream.Documents.Count == 0 ? null : ConvertNode(yamlStream.Documents[0].RootNode);
    }

    private static JsonNode? ConvertNode(YamlNode node) => node switch
    {
        YamlMappingNode mapping => ConvertMapping(mapping),
        YamlSequenceNode sequence => ConvertSequence(sequence),
        YamlScalarNode scalar => ConvertScalar(scalar),
        _ => null,
    };

    private static JsonObject ConvertMapping(YamlMappingNode mapping)
    {
        var obj = new JsonObject();
        foreach (var (keyNode, valueNode) in mapping.Children)
        {
            var key = keyNode is YamlScalarNode keyScalar ? keyScalar.Value ?? string.Empty : keyNode.ToString();
            obj[key] = ConvertNode(valueNode);
        }

        return obj;
    }

    private static JsonArray ConvertSequence(YamlSequenceNode sequence)
    {
        var array = new JsonArray();
        foreach (var item in sequence.Children)
        {
            array.Add(ConvertNode(item));
        }

        return array;
    }

    private static JsonNode? ConvertScalar(YamlScalarNode scalar)
    {
        var value = scalar.Value;

        // Explicitly quoted/block scalars are unambiguously strings; only plain scalars need type inference.
        if (scalar.Style != ScalarStyle.Plain)
        {
            return JsonValue.Create(value);
        }

        if (value is null || value == "~" || value.Equals("null", StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        if (bool.TryParse(value, out var boolValue))
        {
            return JsonValue.Create(boolValue);
        }

        if (JsonNumberPattern.IsMatch(value))
        {
            // Route through JsonNode.Parse rather than hand-picking long/double, so the resulting node
            // is JsonElement-backed exactly like a native JSON number would be. A CLR-boxed JsonValue<long>
            // only supports GetValue<T>() for an exact type match, so a document read from YAML would
            // otherwise behave differently than the same document read from JSON when callers ask for
            // GetValue<int>() on what was written as a plain integer.
            return JsonNode.Parse(value);
        }

        return JsonValue.Create(value);
    }
}
