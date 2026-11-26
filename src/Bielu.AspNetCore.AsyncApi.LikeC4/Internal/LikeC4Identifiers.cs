using System.Text;

namespace Bielu.AspNetCore.AsyncApi.LikeC4.Internal;

/// <summary>
/// Hands out LikeC4 identifiers: camelCase ASCII letters and digits, never a keyword of the LikeC4 language, and each
/// one once.
/// </summary>
internal sealed class LikeC4Identifiers
{
    /// <summary>The keywords of the LikeC4 grammar (1.61), which an element id cannot be.</summary>
    private static readonly HashSet<string> Keywords = new(StringComparer.Ordinal)
    {
        "BottomTop", "LeftRight", "RightLeft", "TopBottom", "add", "agent", "alt", "amber", "and", "auto",
        "autoLayout", "blue", "border", "bottom", "break", "browser", "bucket", "catch", "color", "component",
        "crow", "cylinder", "dashed", "deployment", "deploymentNode", "description", "descriptionFile", "diagram",
        "diamond", "diff", "document", "dot", "dotted", "dynamic", "dynamicPredicateGroup", "element", "else",
        "exclude", "extend", "extends", "finally", "fixed", "from", "global", "gray", "green", "group", "head",
        "icon", "iconColor", "iconPosition", "iconSize", "icons", "if", "import", "include", "includeAncestors",
        "indigo", "instance", "instanceOf", "is", "it", "kind", "large", "left", "lg", "line", "link", "loop", "max",
        "md", "medium", "metadata", "min", "mobile", "model", "multiple", "muted", "navigateTo", "node", "none",
        "normal", "not", "notation", "notes", "odiamond", "odot", "of", "onormal", "opacity", "open", "opt", "or",
        "order", "ortho", "padding", "par", "parallel", "person", "predicate", "predicateGroup", "primary", "queue",
        "rank", "rectangle", "red", "relationship", "remove", "rgb", "rgba", "right", "routing", "same",
        "secondary", "sequence", "shape", "sink", "size", "sizing", "sky", "slate", "sm", "small", "solid",
        "source", "specification", "spline", "storage", "style", "styleGroup", "summary", "tag", "tail", "target",
        "technology", "textSize", "this", "title", "top", "try", "variant", "vee", "view", "views", "when", "where",
        "with", "xl", "xlarge", "xs", "xsmall",
    };

    private readonly HashSet<string> _taken = new(StringComparer.Ordinal);

    /// <summary>An identifier for <paramref name="name"/> no other call has been given.</summary>
    /// <param name="name">What the identifier names, such as <c>Orders API</c> or <c>orders.{id}.created</c>.</param>
    /// <param name="fallback">The identifier to start from when <paramref name="name"/> has no letters or digits.</param>
    public string Next(string name, string fallback)
    {
        var candidate = CamelCase(name);
        if (candidate.Length == 0)
        {
            candidate = fallback;
        }
        else if (char.IsAsciiDigit(candidate[0]))
        {
            candidate = fallback + char.ToUpperInvariant(candidate[0]) + candidate[1..];
        }

        var id = candidate;
        for (var suffix = 2; Keywords.Contains(id) || !_taken.Add(id); suffix++)
        {
            id = candidate + suffix;
        }

        return id;
    }

    /// <summary>
    /// <c>Orders API</c> → <c>ordersAPI</c>, <c>orders.{id}.created</c> → <c>ordersIdCreated</c>: the ASCII letters
    /// and digits, with the first letter of every run but the first capitalized.
    /// </summary>
    internal static string CamelCase(string name)
    {
        var builder = new StringBuilder(name.Length);
        var upperNext = false;
        foreach (var character in name)
        {
            if (!char.IsAsciiLetterOrDigit(character))
            {
                upperNext = builder.Length > 0;
                continue;
            }

            builder.Append(builder.Length == 0 ? char.ToLowerInvariant(character)
                : upperNext ? char.ToUpperInvariant(character)
                : character);
            upperNext = false;
        }

        return builder.ToString();
    }
}
