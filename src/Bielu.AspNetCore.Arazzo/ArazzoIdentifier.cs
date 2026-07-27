using System.Text.RegularExpressions;

namespace Bielu.AspNetCore.Arazzo;

/// <summary>
/// Validates identifiers (source-description names, workflow IDs, step IDs, dependency IDs) against the
/// Arazzo spec's §5.9 <c>identifier-strict</c> production (<c>^[A-Za-z0-9_-]+$</c>) — the same grammar
/// <c>Bielu.Arazzo.Expressions.RuntimeExpressionParser</c> requires when resolving
/// <c>$sourceDescriptions.NAME</c>/<c>$steps.NAME</c>/<c>$workflows.NAME</c> references. An identifier that
/// fails this check would register successfully but fail unpredictably later, when the runtime-expression
/// references built from it are parsed — so it's rejected here instead, at the point the caller supplied it.
/// </summary>
internal static partial class ArazzoIdentifier
{
    [GeneratedRegex(@"^[A-Za-z0-9_-]+$")]
    private static partial Regex StrictPattern();

    /// <summary>Throws <see cref="ArgumentException"/> if <paramref name="value"/> is null, empty, or does not match the identifier-strict grammar.</summary>
    public static void Validate(string value, string paramName)
    {
        ArgumentException.ThrowIfNullOrEmpty(value, paramName);
        if (!StrictPattern().IsMatch(value))
        {
            throw new ArgumentException(
                $"'{value}' is not a valid Arazzo identifier; it must match '^[A-Za-z0-9_-]+$' (letters, digits, '_', and '-' only).",
                paramName);
        }
    }
}
