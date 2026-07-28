using System.Globalization;

namespace Bielu.Overlay;

/// <summary>
/// The Overlay Specification version a document declares via its <c>overlay</c> field. The two versions
/// differ in more than metadata — see <see cref="OverlayVersionExtensions"/> and the apply engine, which
/// gates <c>copy</c>, primitive targets, and array concatenation on this value.
/// </summary>
public enum OverlayVersion
{
    /// <summary>Overlay Specification 1.0.x (17 October 2024).</summary>
    V1_0,

    /// <summary>Overlay Specification 1.1.x (14 January 2026): adds <c>copy</c>, pins <c>target</c> to RFC 9535, and legalizes primitive targets and array concatenation.</summary>
    V1_1,
}

/// <summary>Conversions between <see cref="OverlayVersion"/> and the version strings used in an Overlay document's <c>overlay</c> field.</summary>
public static class OverlayVersionExtensions
{
    /// <summary>Returns the canonical version string an Overlay document would declare for this version, e.g. "1.1.0".</summary>
    /// <param name="version">The version to convert.</param>
    /// <returns>The canonical major.minor.patch version string.</returns>
    public static string ToVersionString(this OverlayVersion version) => version switch
    {
        OverlayVersion.V1_0 => "1.0.0",
        OverlayVersion.V1_1 => "1.1.0",
        _ => throw new ArgumentOutOfRangeException(nameof(version), version, null),
    };

    /// <summary>
    /// Parses a document's <c>overlay</c> field value. Accepts exactly <c>1.0.&lt;patch&gt;</c> or
    /// <c>1.1.&lt;patch&gt;</c> with a non-negative numeric patch component; anything else — including a
    /// prefix match like <c>1.10.0</c> or a malformed patch like <c>1.1foo</c> — is rejected.
    /// </summary>
    /// <param name="value">The version string to parse.</param>
    /// <param name="version">The parsed version when parsing succeeds; otherwise the default value.</param>
    /// <returns><c>true</c> if <paramref name="value"/> is a well-formed 1.0.x or 1.1.x version; otherwise <c>false</c>.</returns>
    public static bool TryParse(string? value, out OverlayVersion version)
    {
        version = default;

        if (value is null)
        {
            return false;
        }

        var parts = value.Split('.');
        if (parts.Length != 3 || !int.TryParse(parts[2], NumberStyles.None, CultureInfo.InvariantCulture, out _))
        {
            return false;
        }

        if (parts[0] == "1" && parts[1] == "1")
        {
            version = OverlayVersion.V1_1;
            return true;
        }

        if (parts[0] == "1" && parts[1] == "0")
        {
            version = OverlayVersion.V1_0;
            return true;
        }

        return false;
    }
}
