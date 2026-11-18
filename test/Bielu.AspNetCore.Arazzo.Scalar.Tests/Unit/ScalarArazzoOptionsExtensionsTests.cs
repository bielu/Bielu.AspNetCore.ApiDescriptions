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
    public void WithArazzoOptions_EndLabelExtension_WritesTheGlobalBeforeThePluginScript()
    {
        // Arrange
        var options = new ScalarOptions();

        // Act
        var result = options.WithArazzoOptions(arazzo => arazzo.EndLabelExtension = "x-page");

        // Assert
        result.ShouldBeSameAs(options);
        var head = options.HeadContent.ShouldNotBeNull();
        var global = head.IndexOf($"window.{ScalarArazzoOptionsExtensions.GlobalVariableName} = {{\"endLabelExtension\":\"x-page\"}};", StringComparison.Ordinal);
        global.ShouldBeGreaterThanOrEqualTo(0);
        global.ShouldBeLessThan(head.IndexOf("src=\"/bielu/scalar/arazzo/plugin.js\"", StringComparison.Ordinal));
    }

    [Fact]
    public void WithArazzoOptions_PagesOptions_AreWrittenToTheGlobal()
    {
        // Arrange
        var options = new ScalarOptions();

        // Act
        options.WithArazzoOptions(arazzo =>
        {
            arazzo.PagesExtension = "x-shop-pages";
            arazzo.PageExtension = "x-page";
            arazzo.LabelExtension = "x-label";
        });

        // Assert
        options.HeadContent.ShouldNotBeNull().ShouldContain(
            $"window.{ScalarArazzoOptionsExtensions.GlobalVariableName} = {{\"pagesExtension\":\"x-shop-pages\",\"pageExtension\":\"x-page\",\"labelExtension\":\"x-label\"}};");
    }

    [Theory]
    [InlineData("pages")]
    [InlineData("page")]
    [InlineData("label")]
    public void WithArazzoOptions_PagesOptionNotAnExtension_Throws(string option)
    {
        // Act / Assert
        Should.Throw<ArgumentException>(() => new ScalarOptions().WithArazzoOptions(arazzo =>
        {
            switch (option)
            {
                case "pages":
                    arazzo.PagesExtension = "shop-pages";
                    break;
                case "page":
                    arazzo.PageExtension = "page";
                    break;
                default:
                    arazzo.LabelExtension = "label";
                    break;
            }
        }));
    }

    [Fact]
    public void WithArazzoOptions_ConfiguredAssetsPathWithoutOptions_WritesNoGlobal()
    {
        // Arrange
        var options = new ScalarOptions();

        // Act
        options.WithArazzoOptions(arazzo => arazzo.AssetsPath = "/custom/arazzo");

        // Assert
        var head = options.HeadContent.ShouldNotBeNull();
        head.ShouldContain("src=\"/custom/arazzo/plugin.js\"");
        head.ShouldNotContain(ScalarArazzoOptionsExtensions.GlobalVariableName);
    }

    [Fact]
    public void WithArazzoOptions_EndLabelExtensionCannotBreakOutOfTheScript()
    {
        // Arrange
        var options = new ScalarOptions();

        // Act
        options.WithArazzoOptions(arazzo => arazzo.EndLabelExtension = "x-</script><script>alert(1)</script>");

        // Assert
        options.HeadContent.ShouldNotBeNull().ShouldNotContain("</script><script>alert(1)");
    }

    [Theory]
    [InlineData("")]
    [InlineData("page")]
    [InlineData("x-")]
    public void WithArazzoOptions_EndLabelExtensionNotAnExtension_Throws(string extension)
    {
        // Act / Assert
        Should.Throw<ArgumentException>(() => new ScalarOptions().WithArazzoOptions(arazzo => arazzo.EndLabelExtension = extension));
    }

    [Fact]
    public void WithArazzoOptions_ThrowsForNullArgumentsForNullArguments()
    {
        // Act / Assert
        Should.Throw<ArgumentNullException>(() => ((ScalarOptions)null!).WithArazzoOptions(_ => { }));
        Should.Throw<ArgumentNullException>(() => new ScalarOptions().WithArazzoOptions(null!));
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
