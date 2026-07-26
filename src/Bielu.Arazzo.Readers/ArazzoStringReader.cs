using System.Text.Json.Nodes;
using Bielu.Arazzo;

namespace Bielu.Arazzo.Readers;

/// <summary>Reads an Arazzo document from a JSON or YAML string, auto-detecting the format by its first non-whitespace character.</summary>
public static class ArazzoStringReader
{
    public static ArazzoReadResult Read(string content, ArazzoReaderSettings? settings = null)
    {
        settings ??= new ArazzoReaderSettings();
        var diagnostics = new ArazzoDiagnostics();
        var ctx = new ParsingContext(settings, diagnostics);

        JsonNode? root;
        try
        {
            root = LooksLikeJson(content)
                ? JsonNode.Parse(content)
                : YamlToJsonNodeConverter.Convert(new StringReader(content));
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
