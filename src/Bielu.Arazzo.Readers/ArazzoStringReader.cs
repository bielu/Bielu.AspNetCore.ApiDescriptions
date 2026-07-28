using System.Text.Json;
using System.Text.Json.Nodes;
using Bielu.Arazzo;
using Bielu.Spec.Shared;

namespace Bielu.Arazzo.Readers;

/// <summary>Reads an Arazzo document from a JSON or YAML string, auto-detecting the format from its content.</summary>
public static class ArazzoStringReader
{
    /// <summary>Reads the Arazzo document encoded in <paramref name="content"/>.</summary>
    /// <param name="content">The JSON or YAML text to read the document from.</param>
    /// <param name="settings">Optional reader settings; defaults are used when omitted.</param>
    /// <returns>The parsed document together with any diagnostics collected while reading it.</returns>
    public static ArazzoReadResult Read(string content, ArazzoReaderSettings? settings = null)
    {
        ArgumentNullException.ThrowIfNull(content);

        settings ??= new ArazzoReaderSettings();
        var diagnostics = new ArazzoDiagnostics();
        var ctx = new ParsingContext(settings, diagnostics);

        JsonNode? root;
        try
        {
            root = ParseRoot(content);
        }
        catch (Exception ex)
        {
            ctx.Error("/", $"Failed to parse document: {ex.Message}");
            return new ArazzoReadResult { Document = null, Diagnostics = diagnostics };
        }

        var document = ArazzoV1Deserializer.Deserialize(root, ctx);
        if (ArazzoVersionExtensions.TryParse(document.Arazzo, out var version))
        {
            diagnostics.Version = version;
        }
        else
        {
            ctx.Warn("/arazzo", $"Unrecognized Arazzo version '{document.Arazzo}'; expected 1.0.x or 1.1.x.");
        }

        return new ArazzoReadResult { Document = document, Diagnostics = diagnostics };
    }

    /// <summary>
    /// Parses <paramref name="content"/> as JSON when it looks like JSON, falling back to YAML if that
    /// fails.
    /// </summary>
    /// <remarks>
    /// The fallback matters because the two formats overlap at the opening brace: a YAML <em>flow
    /// mapping</em> such as <c>{ arazzo: 1.1.0, workflows: [...] }</c> is valid YAML that begins with
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
