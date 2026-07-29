using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using Bielu.Arazzo.Models;

namespace Bielu.Arazzo.Writers;

/// <summary>
/// Hand-rolled block-style YAML emitter — no YamlDotNet dependency in this package (that stays confined
/// to Bielu.Arazzo.Readers). Renders the same <see cref="JsonNode"/> tree
/// <see cref="ArazzoJsonWriter"/> produces via <see cref="ArazzoJsonNodeWriter"/>. Optimized for
/// correctness and round-tripping through <c>Bielu.Arazzo.Readers</c>, not for matching hand-authored
/// formatting or preserving comments.
/// </summary>
public static class ArazzoYamlWriter
{
    private static readonly Regex PlainSafe = new(@"^[A-Za-z_][A-Za-z0-9_\-]*$", RegexOptions.Compiled);

    private static readonly HashSet<string> ReservedWords = new(StringComparer.OrdinalIgnoreCase)
    {
        "true", "false", "null", "yes", "no", "on", "off", "~",
    };

    /// <summary>Serializes <paramref name="document"/> to an Arazzo YAML document.</summary>
    /// <param name="document">The model to serialize.</param>
    /// <returns>The serialized YAML document.</returns>
    public static string Write(IArazzoSerializable document)
    {
        ArgumentNullException.ThrowIfNull(document);

        var nodeWriter = new ArazzoJsonNodeWriter();
        document.SerializeAsV1(nodeWriter);

        var sb = new StringBuilder();
        if (nodeWriter.Result is JsonObject root)
        {
            RenderObject(root, sb, 0);
        }
        else
        {
            RenderScalarOrEmpty(nodeWriter.Result, sb);
        }

        sb.Append('\n');
        return sb.ToString();
    }

    /// <summary>
    /// Renders an object's keys. The caller is always responsible for positioning the cursor for the
    /// FIRST key (column 0 at the document root, right after "- " for an array item, or after the
    /// caller's own newline+indent for a nested property) — this only emits newline+indent before the
    /// 2nd key onward, mirroring how <see cref="RenderArray"/> handles its first item.
    /// </summary>
    private static void RenderObject(JsonObject obj, StringBuilder sb, int indent)
    {
        if (obj.Count == 0)
        {
            sb.Append("{}");
            return;
        }

        var first = true;
        foreach (var (key, value) in obj)
        {
            if (!first)
            {
                sb.Append('\n');
                sb.Append(' ', indent);
            }

            first = false;
            sb.Append(EscapeScalarString(key)).Append(':');
            AppendPropertyValue(value, sb, indent);
        }
    }

    private static void RenderArray(JsonArray array, StringBuilder sb, int indent)
    {
        if (array.Count == 0)
        {
            sb.Append("[]");
            return;
        }

        var first = true;
        foreach (var item in array)
        {
            if (!first)
            {
                sb.Append('\n');
                sb.Append(' ', indent);
            }

            first = false;
            sb.Append('-');

            switch (item)
            {
                case JsonObject childObj when childObj.Count > 0:
                    sb.Append(' ');
                    RenderObject(childObj, sb, indent + 2);
                    break;
                case JsonArray childArr when childArr.Count > 0:
                    sb.Append('\n');
                    sb.Append(' ', indent + 2);
                    RenderArray(childArr, sb, indent + 2);
                    break;
                default:
                    sb.Append(' ');
                    RenderScalarOrEmpty(item, sb);
                    break;
            }
        }
    }

    private static void AppendPropertyValue(JsonNode? value, StringBuilder sb, int indent)
    {
        switch (value)
        {
            case JsonObject obj when obj.Count > 0:
                sb.Append('\n');
                sb.Append(' ', indent + 2);
                RenderObject(obj, sb, indent + 2);
                break;
            case JsonArray arr when arr.Count > 0:
                sb.Append('\n');
                sb.Append(' ', indent);
                RenderArray(arr, sb, indent);
                break;
            default:
                sb.Append(' ');
                RenderScalarOrEmpty(value, sb);
                break;
        }
    }

    private static void RenderScalarOrEmpty(JsonNode? node, StringBuilder sb)
    {
        switch (node)
        {
            case null:
                sb.Append("null");
                break;
            case JsonObject:
                sb.Append("{}");
                break;
            case JsonArray:
                sb.Append("[]");
                break;
            case JsonValue value:
                sb.Append(RenderScalarValue(value));
                break;
        }
    }

    private static string RenderScalarValue(JsonValue value)
    {
        var kind = value.GetValueKind();
        return kind switch
        {
            JsonValueKind.True => "true",
            JsonValueKind.False => "false",
            JsonValueKind.Null => "null",
            JsonValueKind.Number => value.ToJsonString(),
            JsonValueKind.String => EscapeScalarString(value.GetValue<string>()),
            _ => value.ToJsonString(),
        };
    }

    private static string EscapeScalarString(string s)
    {
        if (s.Length > 0 && PlainSafe.IsMatch(s) && !ReservedWords.Contains(s))
        {
            return s;
        }

        // Double-quoted YAML scalars use JSON-compatible escaping; escape by hand to stay AOT/trim-safe
        // rather than reflecting through JsonSerializer.Serialize<string> for a single scalar.
        var sb = new StringBuilder(s.Length + 2).Append('"');
        foreach (var c in s)
        {
            switch (c)
            {
                case '"': sb.Append("\\\""); break;
                case '\\': sb.Append("\\\\"); break;
                case '\n': sb.Append("\\n"); break;
                case '\r': sb.Append("\\r"); break;
                case '\t': sb.Append("\\t"); break;
                default:
                    if (c < ' ')
                    {
                        sb.Append("\\u").Append(((int)c).ToString("x4"));
                    }
                    else
                    {
                        sb.Append(c);
                    }

                    break;
            }
        }

        return sb.Append('"').ToString();
    }
}
