// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Nodes;
using Bielu.Arazzo.SourceResolvers;

namespace Bielu.Arazzo.Cli.Commands;

/// <summary>
/// Renders what a step's target means to the workflow as one comparable string: the operation or channel, then
/// every local <c>$ref</c> it reaches, so a change to a payload schema under <c>components</c> shows up, with
/// properties sorted, so a regenerated document that only reorders them doesn't.
/// </summary>
/// <remarks>
/// <para>
/// Each referenced definition is written once, numbered in the order it is first reached, and every <c>$ref</c>
/// to it is written as that number. The fingerprint so grows with the document, not with the number of paths
/// through its references, a cycle needs no special case, and renaming a component without changing it changes
/// nothing.
/// </para>
/// <para>
/// Documentation-only keywords (<c>description</c>, <c>summary</c>, <c>title</c>, examples, <c>externalDocs</c>
/// and <c>x-</c> extensions) are left out: rewording a description doesn't change what a step sends or receives.
/// </para>
/// </remarks>
internal static class StepTargetFingerprint
{
    private const int MaxDepth = 64;

    private static readonly HashSet<string> IgnoredKeys = new(StringComparer.Ordinal)
    {
        "description", "summary", "title", "example", "examples", "externalDocs",
    };

    // Keywords whose members are names chosen by the author, such as schema property names or AsyncAPI message
    // ids, not keywords: a property or message called "description" is data and must not be dropped.
    private static readonly HashSet<string> NameMaps = new(StringComparer.Ordinal)
    {
        "properties", "patternProperties", "$defs", "definitions", "dependentSchemas",
        "messages", "parameters", "callbacks", "links", "encoding",
    };

    private const string AdditionalOperations = "additionalOperations";

    /// <summary>The fingerprint of <paramref name="target"/>, a node of <paramref name="document"/>.</summary>
    /// <returns>
    /// The fingerprint, and whether it is complete: a definition nested deeper than the traversal limit is cut
    /// short, so two such fingerprints can match although the definitions differ.
    /// </returns>
    public static Result Of(JsonNode? target, JsonNode document)
    {
        var writer = new Writer(document);
        writer.Write(target, 0, membersAreNames: false);
        writer.WriteInheritedParameters(target);
        writer.WriteDefinitions();
        return new Result(writer.ToString(), writer.Complete);
    }

    /// <summary>A fingerprint and whether it covers the whole definition.</summary>
    public readonly record struct Result(string Value, bool Complete);

    private sealed class Writer(JsonNode document)
    {
        private readonly StringBuilder _builder = new();
        private readonly Dictionary<string, int> _ids = new(StringComparer.Ordinal);
        private readonly List<JsonObject> _definitions = [];

        public bool Complete { get; private set; } = true;

        public override string ToString() => _builder.ToString();

        public void Write(JsonNode? node, int depth, bool membersAreNames)
        {
            if (depth > MaxDepth)
            {
                Complete = false;
                _builder.Append('…');
                return;
            }

            switch (node)
            {
                case JsonObject obj:
                    _builder.Append('{');
                    var reference = membersAreNames ? null : Reference(obj);
                    if (reference is not null)
                    {
                        // Keywords beside a $ref, such as a JSON Schema 2020-12 maxLength, constrain it too.
                        _builder.Append("\"$ref\":").Append(reference).Append(',');
                    }

                    foreach (var (key, value) in obj
                                 .Where(p => membersAreNames || (!IsIgnored(p.Key) && (reference is null || p.Key != "$ref")))
                                 .OrderBy(p => p.Key, StringComparer.Ordinal))
                    {
                        _builder.Append(Quote(key)).Append(':');
                        Write(value, depth + 1, !membersAreNames && NameMaps.Contains(key));
                        _builder.Append(',');
                    }

                    _builder.Append('}');
                    break;
                case JsonArray array:
                    _builder.Append('[');
                    foreach (var item in array)
                    {
                        Write(item, depth + 1, membersAreNames: false);
                        _builder.Append(',');
                    }

                    _builder.Append(']');
                    break;
                case null:
                    _builder.Append("null");
                    break;
                default:
                    _builder.Append(node.ToJsonString());
                    break;
            }
        }

        /// <summary>
        /// The number standing for the definition <paramref name="obj"/>'s <c>$ref</c> points at, or
        /// <see langword="null"/> when it has none, or one that can't be followed: a reference into another
        /// document or to nothing is then written as it stands.
        /// </summary>
        private string? Reference(JsonObject obj)
        {
            if (obj["$ref"] is not JsonValue value || !value.TryGetValue<string>(out var reference) || !reference.StartsWith('#'))
            {
                return null;
            }

            if (!_ids.TryGetValue(reference, out var id))
            {
                if (JsonDocumentPointer.EvaluateObject(document, reference[1..]) is not { } resolved)
                {
                    return null;
                }

                id = _definitions.Count;
                _ids[reference] = id;
                _definitions.Add(resolved);
            }

            return $"\"#{id}\"";
        }

        /// <summary>
        /// For an OpenAPI operation, the parameters of its Path Item that it doesn't override by name and location:
        /// they apply to the operation as much as its own.
        /// </summary>
        public void WriteInheritedParameters(JsonNode? target)
        {
            if (document is not JsonObject root || !(root.ContainsKey("openapi") || root.ContainsKey("swagger")) ||
                target is not JsonObject operation || operation.Parent is not JsonObject parent)
            {
                return;
            }

            // An OpenAPI 3.2 additionalOperations entry sits one level deeper than an HTTP method.
            var pathItem = parent.Parent is JsonObject grandparent && parent.GetPropertyName() == AdditionalOperations
                ? grandparent
                : parent;
            if (pathItem?["parameters"] is not JsonArray inherited)
            {
                return;
            }

            var own = (operation["parameters"] as JsonArray ?? [])
                .Select(Key)
                .Where(key => key is not null)
                .ToHashSet();
            var applicable = inherited.Where(parameter => Key(parameter) is not { } key || !own.Contains(key)).ToList();
            if (applicable.Count == 0)
            {
                return;
            }

            _builder.Append("\ninherited parameters=[");
            foreach (var parameter in applicable)
            {
                Write(parameter, 1, membersAreNames: false);
                _builder.Append(',');
            }

            _builder.Append(']');
        }

        private (string Name, string In)? Key(JsonNode? parameter) =>
            parameter is JsonObject obj && JsonDocumentPointer.FollowLocalReferences(document, obj) is { } resolved &&
            resolved["name"] is JsonValue name && name.TryGetValue<string>(out var n) &&
            resolved["in"] is JsonValue location && location.TryGetValue<string>(out var l)
                ? (n, l)
                : null;

        /// <summary>Each referenced definition, once; writing one can reach more, which are appended in turn.</summary>
        public void WriteDefinitions()
        {
            for (var i = 0; i < _definitions.Count; i++)
            {
                _builder.Append("\n#").Append(i).Append('=');
                Write(_definitions[i], 0, membersAreNames: false);
            }
        }
    }

    private static string Quote(string value) => $"\"{JsonEncodedText.Encode(value, JavaScriptEncoder.UnsafeRelaxedJsonEscaping)}\"";

    private static bool IsIgnored(string key) => IgnoredKeys.Contains(key) || key.StartsWith("x-", StringComparison.Ordinal);
}
