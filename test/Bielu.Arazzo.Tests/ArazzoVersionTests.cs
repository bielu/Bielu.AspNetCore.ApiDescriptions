using Bielu.Arazzo;
using Shouldly;
using Xunit;

namespace Bielu.Arazzo.Tests;

public class ArazzoVersionTests
{
    [Theory]
    [InlineData("1.0.0", ArazzoVersion.V1_0)]
    [InlineData("1.0.5", ArazzoVersion.V1_0)]
    [InlineData("1.0.42", ArazzoVersion.V1_0)]
    [InlineData("1.1.0", ArazzoVersion.V1_1)]
    [InlineData("1.1.5", ArazzoVersion.V1_1)]
    [InlineData("1.1.42", ArazzoVersion.V1_1)]
    public void TryParse_ValidPatchVersion_ReturnsTrueWithExpectedVersion(string input, ArazzoVersion expected)
    {
        // Arrange

        // Act
        var result = ArazzoVersionExtensions.TryParse(input, out var version);

        // Assert
        result.ShouldBeTrue();
        version.ShouldBe(expected);
    }

    [Theory]
    [InlineData("1.10.0")]
    [InlineData("1.1foo")]
    [InlineData("1.10.5")]
    [InlineData("2.0.0")]
    [InlineData("1.2.0")]
    [InlineData("1.1")]
    [InlineData("1.1.")]
    [InlineData("1.1.0.1")]
    [InlineData("1.1.abc")]
    [InlineData("1.1.-1")]
    [InlineData("")]
    [InlineData("not-a-version")]
    public void TryParse_InvalidOrPrefixMatchingVersion_ReturnsFalse(string input)
    {
        // Arrange

        // Act
        var result = ArazzoVersionExtensions.TryParse(input, out var version);

        // Assert
        result.ShouldBeFalse();
        version.ShouldBe(default);
    }

    [Fact]
    public void TryParse_NullInput_ReturnsFalse()
    {
        // Arrange

        // Act
        var result = ArazzoVersionExtensions.TryParse(null, out var version);

        // Assert
        result.ShouldBeFalse();
        version.ShouldBe(default);
    }

    [Theory]
    [InlineData(ArazzoVersion.V1_0, "1.0.0")]
    [InlineData(ArazzoVersion.V1_1, "1.1.0")]
    public void ToVersionString_KnownVersion_ReturnsCanonicalString(ArazzoVersion version, string expected)
    {
        // Arrange

        // Act
        var result = version.ToVersionString();

        // Assert
        result.ShouldBe(expected);
    }
}
