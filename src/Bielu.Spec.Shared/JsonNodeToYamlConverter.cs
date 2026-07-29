using System.Text.Json.Nodes;
using YamlDotNet.Serialization;

namespace Bielu.Spec.Shared;

/// <summary>
/// Serializes a <see cref="JsonNode"/> tree as YAML — the inverse of <see cref="YamlToJsonNodeConverter"/>.
/// </summary>
/// <remarks>
/// Needed because tooling that transforms documents does so on a <see cref="JsonNode"/> tree regardless of
/// the source format, so without this a YAML document read in could only ever be written back out as JSON.
/// The tree is projected onto plain dictionaries, lists, and scalars, which YamlDotNet then emits; strings
/// that would otherwise be read back as another type (<c>true</c>, <c>1.0</c>, <c>null</c>) are quoted.
/// </remarks>
public static class JsonNodeToYamlConverter
{
    private static readonly ISerializer Serializer = new SerializerBuilder()
        .WithQuotingNecessaryStrings()
        .Build();

    /// <summary>Serializes <paramref name="node"/> as YAML.</summary>
    /// <param name="node">The tree to serialize. <see langword="null"/> produces an empty document.</param>
    /// <returns>The YAML representation.</returns>
    public static string Serialize(JsonNode? node) => Serializer.Serialize(ToPlainObject(node));

    private static object? ToPlainObject(JsonNode? node) => node switch
    {
        null => null,
        JsonObject obj => obj.ToDictionary(property => property.Key, property => ToPlainObject(property.Value)),
        JsonArray array => array.Select(ToPlainObject).ToList(),
        JsonValue value => ToScalar(value),
        _ => node.ToJsonString(),
    };

    /// <summary>
    /// Unwraps a <see cref="JsonValue"/> to the narrowest CLR type it round-trips through.
    /// </summary>
    /// <remarks>
    /// Order matters: a <see cref="System.Text.Json.JsonElement"/>-backed value answers
    /// <c>TryGetValue</c> for the type it actually holds, so booleans are probed before integers and
    /// integers before floating point, leaving <see cref="string"/> as the fallback.
    /// </remarks>
    private static object? ToScalar(JsonValue value)
    {
        if (value.TryGetValue<bool>(out var boolean))
        {
            return boolean;
        }

        if (value.TryGetValue<long>(out var integer))
        {
            return integer;
        }

        if (value.TryGetValue<decimal>(out var @decimal))
        {
            return @decimal;
        }

        if (value.TryGetValue<double>(out var floating))
        {
            return floating;
        }

        if (value.TryGetValue<string>(out var text))
        {
            return text;
        }

        return value.ToJsonString();
    }
}
