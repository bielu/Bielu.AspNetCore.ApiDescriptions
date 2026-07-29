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
        // Act
        var parsed = OverlayVersionExtensions.TryParse(value, out var version);

        // Assert
        parsed.ShouldBeTrue();
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
        // Act
        var parsed = OverlayVersionExtensions.TryParse(value, out _);

        // Assert
        parsed.ShouldBeFalse();
    }

    [Theory]
    [InlineData(OverlayVersion.V1_0, "1.0.0")]
    [InlineData(OverlayVersion.V1_1, "1.1.0")]
    public void ToVersionString_RoundTrips(OverlayVersion version, string expected)
    {
        // Act
        var text = version.ToVersionString();
        var parsed = OverlayVersionExtensions.TryParse(text, out var roundTripped);

        // Assert
        text.ShouldBe(expected);
        parsed.ShouldBeTrue();
        roundTripped.ShouldBe(version);
    }
}
