using System.Text;
using System.Text.RegularExpressions;
using System.Xml.Linq;

namespace Bielu.AspNetCore.AsyncApi.Services.XmlDocs;

/// <summary>
/// Renders the content of an XML documentation element (<c>summary</c>, <c>remarks</c>, <c>param</c>) as
/// plain text for AsyncAPI descriptions.
/// </summary>
/// <remarks>
/// Inline tags become the text they stand for: <c>see cref</c> the referenced name, <c>see langword</c> the
/// keyword, <c>see href</c> its link text or URL, <c>paramref</c>/<c>typeparamref</c> the name. Line breaks
/// and indentation from the source comment collapse to single spaces; a blank line or <c>para</c> starts a
/// new paragraph, <c>br</c> a new line and each list <c>item</c> a "- " line. <c>code</c> is kept verbatim.
/// </remarks>
internal static partial class XmlDocumentationText
{
    private const string ParagraphBreak = "\n\n";

    public static string? Render(XElement? element)
    {
        if (element is null)
        {
            return null;
        }

        var builder = new StringBuilder();
        AppendNodes(builder, element);
        var text = ExtraBreaks().Replace(builder.ToString(), ParagraphBreak).Trim();
        return text.Length == 0 ? null : text;
    }

    private static void AppendNodes(StringBuilder builder, XElement element)
    {
        foreach (var node in element.Nodes())
        {
            switch (node)
            {
                case XText text:
                    AppendText(builder, Whitespace().Replace(text.Value, CollapseWhitespace));
                    break;
                case XElement child:
                    AppendElement(builder, child);
                    break;
            }
        }
    }

    private static void AppendElement(StringBuilder builder, XElement element)
    {
        switch (element.Name.LocalName)
        {
            case "para":
                AppendBreak(builder, ParagraphBreak);
                AppendNodes(builder, element);
                AppendBreak(builder, ParagraphBreak);
                break;
            case "br":
                AppendBreak(builder, "\n");
                break;
            case "code":
                AppendBreak(builder, ParagraphBreak);
                builder.Append(element.Value.Trim('\r', '\n'));
                AppendBreak(builder, ParagraphBreak);
                break;
            case "list":
                AppendBreak(builder, ParagraphBreak);
                AppendNodes(builder, element);
                AppendBreak(builder, ParagraphBreak);
                break;
            case "item":
                AppendBreak(builder, "\n");
                builder.Append("- ");
                AppendNodes(builder, element);
                break;
            case "see" or "seealso" when element.Nodes().Any():
                AppendNodes(builder, element);
                break;
            case "see" or "seealso":
                AppendText(builder, ReferenceText(element));
                break;
            case "paramref" or "typeparamref":
                AppendText(builder, element.Attribute("name")?.Value ?? string.Empty);
                break;
            default:
                AppendNodes(builder, element);
                break;
        }
    }

    private static string ReferenceText(XElement element)
    {
        if (element.Attribute("cref")?.Value is { Length: > 0 } cref)
        {
            return CrefName(cref);
        }

        return element.Attribute("langword")?.Value ?? element.Attribute("href")?.Value ?? string.Empty;
    }

    /// <summary>
    /// Shortens a documentation id to a readable name: a type to its own name (<c>T:A.B.List`1</c> is
    /// <c>List</c>), a member to its type and member name (<c>M:A.B.Service.Send(System.String)</c> is
    /// <c>Service.Send</c>).
    /// </summary>
    private static string CrefName(string cref)
    {
        var kind = cref.Length > 2 && cref[1] == ':' ? cref[0] : 'T';
        var name = cref.Length > 2 && cref[1] == ':' ? cref[2..] : cref;

        var parameters = name.IndexOf('(');
        if (parameters >= 0)
        {
            name = name[..parameters];
        }

        var segments = GenericArity().Replace(name, string.Empty).Split('.');
        return kind is 'T' or 'N' || segments.Length < 2
            ? segments[^1]
            : $"{segments[^2]}.{segments[^1]}";
    }

    private static void AppendText(StringBuilder builder, string text)
    {
        // Text after a line break doesn't start with the indentation the space collapse left behind.
        builder.Append(builder.Length == 0 || builder[^1] == '\n' ? text.TrimStart(' ') : text);
    }

    private static void AppendBreak(StringBuilder builder, string lineBreak)
    {
        while (builder.Length > 0 && builder[^1] == ' ')
        {
            builder.Length--;
        }

        if (builder.Length > 0)
        {
            builder.Append(lineBreak);
        }
    }

    private static string CollapseWhitespace(Match match)
    {
        return match.Value.Count(c => c == '\n') >= 2 ? ParagraphBreak : " ";
    }

    [GeneratedRegex(@"\s+")]
    private static partial Regex Whitespace();

    [GeneratedRegex(@"[ \t]*\n(?:[ \t]*\n)+")]
    private static partial Regex ExtraBreaks();

    [GeneratedRegex(@"`+\d+")]
    private static partial Regex GenericArity();
}
