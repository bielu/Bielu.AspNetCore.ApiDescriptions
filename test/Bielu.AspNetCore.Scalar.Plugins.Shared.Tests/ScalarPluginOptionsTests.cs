using Shouldly;
using Xunit;

namespace Bielu.AspNetCore.Scalar.Plugins.Tests;

public class ScalarPluginOptionsTests
{
    [Theory]
    [InlineData("x-page", true)]
    [InlineData("x-a", true)]
    [InlineData("x-", false)]
    [InlineData("page", false)]
    [InlineData("X-page", false)]
    [InlineData("", false)]
    public void IsSpecificationExtension_ChecksTheXPrefixAndAName(string value, bool expected)
    {
        // Act
        var result = ScalarPluginOptions.IsSpecificationExtension(value);

        // Assert
        result.ShouldBe(expected);
    }

    [Fact]
    public void SpecificationExtensions_KeepsSetOptionsInOrder_ByTheirJavaScriptNames()
    {
        // Act
        var options = ScalarPluginOptions.SpecificationExtensions(
        [
            ("PagesExtension", "x-shop-pages"),
            ("EndLabelExtension", null),
            ("PageExtension", "x-page"),
        ], "configure");

        // Assert
        options.ShouldBe(
        [
            new KeyValuePair<string, string>("pagesExtension", "x-shop-pages"),
            new KeyValuePair<string, string>("pageExtension", "x-page"),
        ]);
    }

    [Fact]
    public void SpecificationExtensions_NoneSet_ReturnsEmpty()
    {
        // Act
        var options = ScalarPluginOptions.SpecificationExtensions([("PageExtension", null)], "configure");

        // Assert
        options.ShouldBeEmpty();
    }

    [Fact]
    public void SpecificationExtensions_ThrowsForAValueThatIsNotAnExtension_NamingTheProperty()
    {
        // Act
        var exception = Should.Throw<ArgumentException>(() =>
            ScalarPluginOptions.SpecificationExtensions([("LabelExtension", "label")], "configure"));

        // Assert
        exception.ParamName.ShouldBe("configure");
        exception.Message.ShouldStartWith("LabelExtension 'label' is not a specification extension: it must be 'x-' followed by a name.");
    }

    [Fact]
    public void SpecificationExtensions_ThrowsForNullOptions()
    {
        // Act & Assert
        Should.Throw<ArgumentNullException>(() => ScalarPluginOptions.SpecificationExtensions(null!, "configure"));
    }

    [Fact]
    public void ToGlobalScript_WritesTheOptionsAsAJsonObjectInOrder()
    {
        // Act
        var script = ScalarPluginOptions.ToGlobalScript("__BIELU_TEST__",
        [
            new("pagesExtension", "x-shop-pages"),
            new("pageExtension", "x-page"),
        ]);

        // Assert
        script.ShouldBe("<script>window.__BIELU_TEST__ = {\"pagesExtension\":\"x-shop-pages\",\"pageExtension\":\"x-page\"};</script>");
    }

    [Fact]
    public void ToGlobalScript_EscapesMarkupSoAValueCannotCloseTheScript()
    {
        // Act
        var script = ScalarPluginOptions.ToGlobalScript("__BIELU_TEST__", [new("pageExtension", "x-</script><b>&")]);

        // Assert
        script.ShouldNotContain("</script><b>");
        script.ShouldContain("\\u003C/script\\u003E\\u003Cb\\u003E\\u0026");
    }

    [Fact]
    public void ToGlobalScript_NoOptions_ReturnsEmpty()
    {
        // Act & Assert
        ScalarPluginOptions.ToGlobalScript("__BIELU_TEST__", []).ShouldBeEmpty();
    }

    [Fact]
    public void ToGlobalScript_ThrowsForAMissingGlobalName()
    {
        // Act & Assert
        Should.Throw<ArgumentException>(() => ScalarPluginOptions.ToGlobalScript("", []));
    }

    [Fact]
    public void WithQuery_NoParameters_ReturnsTheUrlAsItIs()
    {
        // Act & Assert
        ScalarPluginOptions.WithQuery("https://cdn/p.mjs?a=1#top", []).ShouldBe("https://cdn/p.mjs?a=1#top");
    }

    [Fact]
    public void WithQuery_AddsParameters_KeepingOthersAndTheFragment()
    {
        // Act
        var url = ScalarPluginOptions.WithQuery("https://cdn/p.mjs?v=2&pageExtension=x-old#top",
        [
            new("pageExtension", "x-page"),
            new("labelExtension", "x-label"),
        ]);

        // Assert
        url.ShouldBe("https://cdn/p.mjs?v=2&pageExtension=x-page&labelExtension=x-label#top");
    }

    [Fact]
    public void WithQuery_AddsAQueryToAUrlWithout_AndEscapesValues()
    {
        // Act
        var url = ScalarPluginOptions.WithQuery("https://cdn/p.mjs", [new("labelExtension", "x-a b&c")]);

        // Assert
        url.ShouldBe("https://cdn/p.mjs?labelExtension=x-a%20b%26c");
    }

    [Fact]
    public void WithQuery_ThrowsForNullArguments()
    {
        // Act & Assert
        Should.Throw<ArgumentNullException>(() => ScalarPluginOptions.WithQuery(null!, []));
        Should.Throw<ArgumentNullException>(() => ScalarPluginOptions.WithQuery("https://cdn/p.mjs", null!));
    }
}
