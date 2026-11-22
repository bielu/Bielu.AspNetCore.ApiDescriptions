// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Nodes;
using Bielu.Arazzo.SourceResolvers;

namespace Bielu.Arazzo.Cli.Commands;

/// <summary>
/// Renders what a step's target means to the workflow as one comparable string: the operation or channel with
/// every local <c>$ref</c> inlined, so a change to a payload schema under <c>components</c> shows up, and with
/// properties sorted, so a regenerated document that only reorders them doesn't.
/// </summary>
/// <remarks>
/// Documentation-only keywords (<c>description</c>, <c>summary</c>, <c>title</c>, examples, <c>externalDocs</c>
/// and <c>x-</c> extensions) are left out: rewording a description doesn't change what a step sends or receives.
/// </remarks>
internal static class StepTargetFingerprint
{
    private const int MaxDepth = 64;

    private static readonly HashSet<string> IgnoredKeys = new(StringComparer.Ordinal)
    {
        "description", "summary", "title", "example", "examples", "externalDocs",
    };

    // Keywords whose members are names chosen by the author, such as schema property names, not keywords: a
    // property called "description" is data and must not be dropped.
    private static readonly HashSet<string> NameMaps = new(StringComparer.Ordinal)
    {
        "properties", "patternProperties", "$defs", "definitions", "dependentSchemas",
    };

    public static string Of(JsonNode? target, JsonNode document)
    {
        var builder = new StringBuilder();
        Write(target, document, builder, new HashSet<string>(StringComparer.Ordinal), 0, membersAreNames: false);
        return builder.ToString();
    }

    private static void Write(
        JsonNode? node, JsonNode document, StringBuilder builder, HashSet<string> refsInProgress, int depth, bool membersAreNames)
    {
        if (depth > MaxDepth)
        {
            builder.Append("…");
            return;
        }

        switch (node)
        {
            case JsonObject obj when obj["$ref"] is JsonValue refValue && refValue.TryGetValue<string>(out var reference):
                WriteReference(reference, document, builder, refsInProgress, depth);
                break;
            case JsonObject obj:
                builder.Append('{');
                foreach (var (key, value) in obj.Where(p => membersAreNames || !IsIgnored(p.Key)).OrderBy(p => p.Key, StringComparer.Ordinal))
                {
                    builder.Append(Quote(key)).Append(':');
                    Write(value, document, builder, refsInProgress, depth + 1, !membersAreNames && NameMaps.Contains(key));
                    builder.Append(',');
                }

                builder.Append('}');
                break;
            case JsonArray array:
                builder.Append('[');
                foreach (var item in array)
                {
                    Write(item, document, builder, refsInProgress, depth + 1, membersAreNames: false);
                    builder.Append(',');
                }

                builder.Append(']');
                break;
            case null:
                builder.Append("null");
                break;
            default:
                builder.Append(node.ToJsonString());
                break;
        }
    }

    private static void WriteReference(string reference, JsonNode document, StringBuilder builder, HashSet<string> refsInProgress, int depth)
    {
        // Only a reference into the same document can be followed; a cyclic one is written as the reference itself.
        if (!reference.StartsWith('#') || refsInProgress.Contains(reference) ||
            JsonDocumentPointer.EvaluateObject(document, reference[1..]) is not { } resolved)
        {
            builder.Append("{\"$ref\":").Append(Quote(reference)).Append('}');
            return;
        }

        refsInProgress.Add(reference);
        Write(resolved, document, builder, refsInProgress, depth + 1, membersAreNames: false);
        refsInProgress.Remove(reference);
    }

    private static string Quote(string value) => $"\"{JsonEncodedText.Encode(value, JavaScriptEncoder.UnsafeRelaxedJsonEscaping)}\"";

    private static bool IsIgnored(string key) => IgnoredKeys.Contains(key) || key.StartsWith("x-", StringComparison.Ordinal);
}
