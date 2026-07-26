namespace Bielu.Arazzo;

/// <summary>
/// The Arazzo Specification version a document declares via its <c>arazzo</c> field.
/// Per spec section 5.1, tooling SHOULD NOT distinguish patch versions, so only major.minor is modeled.
/// </summary>
public enum ArazzoVersion
{
    V1_0,
    V1_1,
}

public static class ArazzoVersionExtensions
{
    public static string ToVersionString(this ArazzoVersion version) => version switch
    {
        ArazzoVersion.V1_0 => "1.0.0",
        ArazzoVersion.V1_1 => "1.1.0",
        _ => throw new ArgumentOutOfRangeException(nameof(version), version, null),
    };

    public static bool TryParse(string? value, out ArazzoVersion version)
    {
        if (value is not null && value.StartsWith("1.1", StringComparison.Ordinal))
        {
            version = ArazzoVersion.V1_1;
            return true;
        }

        if (value is not null && value.StartsWith("1.0", StringComparison.Ordinal))
        {
            version = ArazzoVersion.V1_0;
            return true;
        }

        version = default;
        return false;
    }
}
