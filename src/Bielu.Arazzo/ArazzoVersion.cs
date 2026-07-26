using System.Globalization;

namespace Bielu.Arazzo;

/// <summary>
/// The Arazzo Specification version a document declares via its <c>arazzo</c> field.
/// Per spec section 5.1, tooling SHOULD NOT distinguish patch versions, so only major.minor is modeled.
/// </summary>
public enum ArazzoVersion
{
    /// <summary>Arazzo Specification 1.0.x.</summary>
    V1_0,

    /// <summary>Arazzo Specification 1.1.x.</summary>
    V1_1,
}

/// <summary>Conversions between <see cref="ArazzoVersion"/> and the version strings used in an Arazzo document's <c>arazzo</c> field.</summary>
public static class ArazzoVersionExtensions
{
    /// <summary>Returns the canonical version string an Arazzo document would declare for this version, e.g. "1.1.0".</summary>
    /// <param name="version">The version to convert.</param>
    /// <returns>The canonical major.minor.patch version string.</returns>
    public static string ToVersionString(this ArazzoVersion version) => version switch
    {
        ArazzoVersion.V1_0 => "1.0.0",
        ArazzoVersion.V1_1 => "1.1.0",
        _ => throw new ArgumentOutOfRangeException(nameof(version), version, null),
    };

    /// <summary>
    /// Parses a document's <c>arazzo</c> field value. Accepts exactly <c>1.0.&lt;patch&gt;</c> or
    /// <c>1.1.&lt;patch&gt;</c> with a non-negative numeric patch component; anything else — including a
    /// prefix match like <c>1.10.0</c> or a malformed patch like <c>1.1foo</c> — is rejected.
    /// </summary>
    /// <param name="value">The version string to parse.</param>
    /// <param name="version">The parsed version when parsing succeeds; otherwise the default value.</param>
    /// <returns><c>true</c> if <paramref name="value"/> is a well-formed 1.0.x or 1.1.x version; otherwise <c>false</c>.</returns>
    public static bool TryParse(string? value, out ArazzoVersion version)
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
            version = ArazzoVersion.V1_1;
            return true;
        }

        if (parts[0] == "1" && parts[1] == "0")
        {
            version = ArazzoVersion.V1_0;
            return true;
        }

        return false;
    }
}
