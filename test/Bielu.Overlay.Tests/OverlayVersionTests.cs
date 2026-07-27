using Shouldly;
using Xunit;

namespace Bielu.Overlay.Tests;

public class OverlayVersionTests
{
    [Theory]
    [InlineData("1.0.0", OverlayVersion.V1_0)]
    [InlineData("1.0.7", OverlayVersion.V1_0)]
    [InlineData("1.1.0", OverlayVersion.V1_1)]
    [InlineData("1.1.42", OverlayVersion.V1_1)]
    public void TryParse_AcceptsKnownVersions(string value, OverlayVersion expected)
    {
        OverlayVersionExtensions.TryParse(value, out var version).ShouldBeTrue();
        version.ShouldBe(expected);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("1.0")]
    [InlineData("1.2.0")]
    [InlineData("2.0.0")]
    [InlineData("1.10.0")]   // must not prefix-match 1.1
    [InlineData("1.1foo")]
    [InlineData("1.1.x")]
    [InlineData("1.1.-1")]
    public void TryParse_RejectsEverythingElse(string? value)
    {
        OverlayVersionExtensions.TryParse(value, out _).ShouldBeFalse();
    }

    [Theory]
    [InlineData(OverlayVersion.V1_0, "1.0.0")]
    [InlineData(OverlayVersion.V1_1, "1.1.0")]
    public void ToVersionString_RoundTrips(OverlayVersion version, string expected)
    {
        version.ToVersionString().ShouldBe(expected);
        OverlayVersionExtensions.TryParse(expected, out var parsed).ShouldBeTrue();
        parsed.ShouldBe(version);
    }
}
