using System.Text.Json;
using System.Text.Json.Nodes;
using Bielu.Spec.Shared;

namespace Bielu.Overlay.Readers;

/// <summary>Reads an Overlay document from a JSON or YAML string, auto-detecting the format by its first non-whitespace character.</summary>
public static class OverlayStringReader
{
    /// <summary>Reads the Overlay document encoded in <paramref name="content"/>.</summary>
    /// <param name="content">The JSON or YAML text to read the document from.</param>
    /// <param name="settings">Optional reader settings; defaults are used when omitted.</param>
    /// <returns>The parsed document together with any diagnostics collected while reading it.</returns>
    public static OverlayReadResult Read(string content, OverlayReaderSettings? settings = null)
    {
        ArgumentNullException.ThrowIfNull(content);

        settings ??= new OverlayReaderSettings();
        var diagnostics = new List<OverlayDiagnostic>();
        var ctx = new ParsingContext(settings, diagnostics);

        JsonNode? root;
        try
        {
            root = ParseRoot(content);
        }
        catch (Exception ex)
        {
            // Reading never throws for malformed input — the caller gets diagnostics instead.
            ctx.Error("/", $"Failed to parse document: {ex.Message}");
            return new OverlayReadResult { Document = null, Diagnostics = diagnostics };
        }

        var document = OverlayV1Deserializer.Deserialize(root, ctx);

        if (!OverlayVersionExtensions.TryParse(document.Overlay, out _) && document.Overlay.Length > 0)
        {
            ctx.Warn("/overlay", $"Unrecognized Overlay version '{document.Overlay}'; expected 1.0.x or 1.1.x.");
        }

        return new OverlayReadResult { Document = document, Diagnostics = diagnostics };
    }

    /// <summary>
    /// Parses <paramref name="content"/> as JSON when it looks like JSON, falling back to YAML if that
    /// fails.
    /// </summary>
    /// <remarks>
    /// The fallback matters because the two formats overlap at the opening brace: a YAML <em>flow
    /// mapping</em> such as <c>{ overlay: 1.1.0, actions: [...] }</c> is valid YAML that begins with
    /// <c>{</c>, so a first-character sniff alone would route it to the JSON parser and fail it. JSON is
    /// still tried first, so genuine JSON never pays for the YAML parser.
    /// </remarks>
    private static JsonNode? ParseRoot(string content)
    {
        if (LooksLikeJson(content))
        {
            try
            {
                return JsonNode.Parse(content);
            }
            catch (JsonException)
            {
                // Not JSON after all — fall through and let the YAML parser try.
            }
        }

        return YamlToJsonNodeConverter.Convert(new StringReader(content));
    }

    private static bool LooksLikeJson(string content)
    {
        foreach (var c in content)
        {
            if (char.IsWhiteSpace(c))
            {
                continue;
            }

            return c is '{' or '[';
        }

        return false;
    }
}
