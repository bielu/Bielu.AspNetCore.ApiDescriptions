using Scalar.AspNetCore;
using Shouldly;
using Xunit;

namespace Bielu.AspNetCore.Arazzo.Scalar.Tests.Unit;

public class ScalarArazzoOptionsExtensionsTests
{
    [Fact]
    public void WithArazzo_InjectsPluginScript()
    {
        // Arrange
        var options = new ScalarOptions();

        // Act
        var result = options.WithArazzo();

        // Assert
        result.ShouldBeSameAs(options);
        options.HeadContent.ShouldNotBeNull();
        options.HeadContent.ShouldContain("src=\"/bielu/scalar/arazzo/plugin.js\"");
        // The bundle reads its documents from Scalar's sources, so no global override is written.
        options.HeadContent.ShouldNotContain(ScalarArazzoOptionsExtensions.GlobalVariableName);
    }

    [Fact]
    public void WithArazzo_CustomAssetsPath_IsUsedForTheScriptTag()
    {
        // Arrange
        var options = new ScalarOptions();

        // Act
        options.WithArazzo("/custom/arazzo/");

        // Assert
        options.HeadContent.ShouldNotBeNull();
        options.HeadContent.ShouldContain("src=\"/custom/arazzo/plugin.js\"");
    }

    [Fact]
    public void WithArazzo_ThrowsForNullOptions()
    {
        // Arrange
        ScalarOptions options = null!;

        // Act / Assert
        Should.Throw<ArgumentNullException>(() => options.WithArazzo());
    }

    [Fact]
    public void WithArazzo_ThrowsForEmptyAssetsPath()
    {
        // Arrange
        var options = new ScalarOptions();

        // Act / Assert
        Should.Throw<ArgumentException>(() => options.WithArazzo(string.Empty));
    }

    [Fact]
    public void AddArazzoDocument_ReturnsTheSameOptions()
    {
        // Arrange
        var options = new ScalarOptions();

        // Act
        var result = options.AddArazzoDocument("v1", "Workflows");

        // Assert
        result.ShouldBeSameAs(options);
    }

    [Fact]
    public void AddArazzoDocument_ThrowsForNullOptions()
    {
        // Arrange
        ScalarOptions options = null!;

        // Act / Assert
        Should.Throw<ArgumentNullException>(() => options.AddArazzoDocument());
    }

    [Theory]
    [InlineData("", ArazzoDefaults.DefaultArazzoRoute)]
    [InlineData("v1", "")]
    public void AddArazzoDocument_ThrowsForEmptyArguments(string documentName, string routePattern)
    {
        // Arrange
        var options = new ScalarOptions();

        // Act / Assert
        Should.Throw<ArgumentException>(() => options.AddArazzoDocument(documentName, routePattern: routePattern));
    }
}
