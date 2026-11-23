using System.Reflection;
using Aspire.Hosting.ApplicationModel;
using Aspire.Hosting.Testing;
using Bielu.AspNetCore.Arazzo.Scalar.Aspire;
using global::Scalar.Aspire;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Shouldly;
using Xunit;

namespace Bielu.AspNetCore.AsyncApi.Scalar.Aspire.Tests.Unit;

/// <summary>
/// Unit tests for the <c>WithArazzo</c> and <c>AddArazzoDocument</c> AppHost extensions. They only shape the Aspire
/// app model, so nothing is started.
/// </summary>
public class ScalarArazzoAspireExtensionsTests
{
    private static async Task<IList<string>> GetPluginUrlsAsync(Action<IResourceBuilder<ScalarResource>> configure)
    {
        await using var builder = DistributedApplicationTestingBuilder.Create();
        var scalar = builder.AddScalarApiReference();
        configure(scalar);

        await using var provider = builder.Services.BuildServiceProvider();
        var pluginUrls = provider.GetRequiredService<IOptionsMonitor<ScalarAspireOptions>>().Get(scalar.Resource.Name).PluginUrls;
        pluginUrls.ShouldNotBeNull();
        return pluginUrls;
    }

    // Scalar.Aspire keeps the added documents in an internal list; ScalarDocument itself is public.
    private static IReadOnlyList<ScalarDocument> GetDocuments(ScalarOptions options) =>
        (List<ScalarDocument>)typeof(ScalarOptions)
            .GetProperty("Documents", BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public)!
            .GetValue(options)!;

    [Fact]
    public async Task WithArazzo_Default_AddsThePinnedPluginUrl()
    {
        // Act
        var pluginUrls = await GetPluginUrlsAsync(scalar => scalar.WithArazzo());

        // Assert
        pluginUrls.ShouldHaveSingleItem().ShouldBe(ScalarArazzoAspireExtensions.DefaultPluginUrl);
        ScalarArazzoAspireExtensions.DefaultPluginUrl.ShouldStartWith("https://cdn.jsdelivr.net/npm/@bielu/scalar-arazzo@");
        ScalarArazzoAspireExtensions.DefaultPluginUrl.ShouldEndWith("/dist/scalar-plugin.mjs");
    }

    [Fact]
    public async Task WithArazzo_CustomUrl_AddsIt()
    {
        // Act
        var pluginUrls = await GetPluginUrlsAsync(scalar => scalar.WithArazzo("https://cdn.example.com/arazzo.mjs"));

        // Assert
        pluginUrls.ShouldHaveSingleItem().ShouldBe("https://cdn.example.com/arazzo.mjs");
    }

    [Fact]
    public async Task WithArazzo_WithAConsole_KeepsBothPlugins()
    {
        // Act
        var pluginUrls = await GetPluginUrlsAsync(scalar => scalar
            .WithArazzo()
            .WithArazzo("https://cdn.example.com/other.mjs"));

        // Assert
        pluginUrls.ShouldBe([ScalarArazzoAspireExtensions.DefaultPluginUrl, "https://cdn.example.com/other.mjs"]);
    }

    [Fact]
    public async Task WithArazzo_InvalidArguments_Throw()
    {
        // Arrange
        await using var builder = DistributedApplicationTestingBuilder.Create();
        var scalar = builder.AddScalarApiReference();

        // Act & Assert
        Should.Throw<ArgumentNullException>(() => ScalarArazzoAspireExtensions.WithArazzo(null!));
        Should.Throw<ArgumentException>(() => scalar.WithArazzo(pluginUrl: ""));
    }

    [Fact]
    public async Task WithArazzoOptions_NoOptions_AddsThePinnedPluginUrlUnchanged()
    {
        // Act
        var pluginUrls = await GetPluginUrlsAsync(scalar => scalar.WithArazzoOptions(_ => { }));

        // Assert
        pluginUrls.ShouldHaveSingleItem().ShouldBe(ScalarArazzoAspireExtensions.DefaultPluginUrl);
    }

    [Fact]
    public async Task WithArazzoOptions_AllOptions_EncodesThemInThePluginUrl()
    {
        // Act
        var pluginUrls = await GetPluginUrlsAsync(scalar => scalar.WithArazzoOptions(arazzo =>
        {
            arazzo.EndLabelExtension = "x-end";
            arazzo.PagesExtension = "x-shop pages";
            arazzo.PageExtension = "x-page";
            arazzo.LabelExtension = "x-label&text";
            arazzo.ActorExtension = "x-actor";
            arazzo.ActorsExtension = "x-actors";
        }));

        // Assert
        var url = new Uri(pluginUrls.ShouldHaveSingleItem());
        url.GetLeftPart(UriPartial.Path).ShouldBe(ScalarArazzoAspireExtensions.DefaultPluginUrl);
        var query = System.Web.HttpUtility.ParseQueryString(url.Query);
        query["endLabelExtension"].ShouldBe("x-end");
        query["pagesExtension"].ShouldBe("x-shop pages");
        query["pageExtension"].ShouldBe("x-page");
        query["labelExtension"].ShouldBe("x-label&text");
        query["actorExtension"].ShouldBe("x-actor");
        query["actorsExtension"].ShouldBe("x-actors");
    }

    [Fact]
    public async Task WithArazzoOptions_CustomUrl_KeepsItsQueryAndFragment()
    {
        // Act
        var pluginUrls = await GetPluginUrlsAsync(scalar => scalar.WithArazzoOptions(arazzo =>
        {
            arazzo.PluginUrl = "https://cdn.example.com/arazzo.mjs?v=2&pageExtension=x-old#top";
            arazzo.PageExtension = "x-page";
        }));

        // Assert
        pluginUrls.ShouldHaveSingleItem().ShouldBe("https://cdn.example.com/arazzo.mjs?v=2&pageExtension=x-page#top");
    }

    [Theory]
    [InlineData("end", "x-")]
    [InlineData("end", "page")]
    [InlineData("pages", "x-")]
    [InlineData("page", "page")]
    [InlineData("label", "x-")]
    [InlineData("actor", "x-")]
    [InlineData("actor", "actor")]
    [InlineData("actors", "x-")]
    public async Task WithArazzoOptions_BarePrefixOrNonExtension_Throws(string option, string value)
    {
        // Arrange
        await using var builder = DistributedApplicationTestingBuilder.Create();
        var scalar = builder.AddScalarApiReference();

        // Act & Assert
        var exception = Should.Throw<ArgumentException>(() => scalar.WithArazzoOptions(arazzo =>
        {
            switch (option)
            {
                case "end":
                    arazzo.EndLabelExtension = value;
                    break;
                case "pages":
                    arazzo.PagesExtension = value;
                    break;
                case "page":
                    arazzo.PageExtension = value;
                    break;
                case "actor":
                    arazzo.ActorExtension = value;
                    break;
                case "actors":
                    arazzo.ActorsExtension = value;
                    break;
                default:
                    arazzo.LabelExtension = value;
                    break;
            }
        }));
        exception.Message.ShouldContain("'x-' followed by a name");
    }

    [Fact]
    public async Task WithArazzoOptions_InvalidArguments_Throw()
    {
        // Arrange
        await using var builder = DistributedApplicationTestingBuilder.Create();
        var scalar = builder.AddScalarApiReference();

        // Act & Assert
        Should.Throw<ArgumentNullException>(() => ScalarArazzoAspireExtensions.WithArazzoOptions(null!, _ => { }));
        Should.Throw<ArgumentNullException>(() => scalar.WithArazzoOptions(null!));
        Should.Throw<ArgumentException>(() => scalar.WithArazzoOptions(arazzo => arazzo.PluginUrl = ""));
    }

    [Fact]
    public void AddArazzoDocument_Defaults_AddsTheMapArazzoRoute()
    {
        // Arrange
        var options = new ScalarOptions();

        // Act
        var result = options.AddArazzoDocument();

        // Assert
        result.ShouldBeSameAs(options);
        var document = GetDocuments(options).ShouldHaveSingleItem();
        document.Name.ShouldBe("v1");
        document.Title.ShouldBeNull();
        document.RoutePattern.ShouldBe("/arazzo/{documentName}.json");
        document.IsDefault.ShouldBeFalse();
    }

    [Fact]
    public void AddArazzoDocument_NextToOpenApi_AddsASecondSource()
    {
        // Arrange
        var options = new ScalarOptions();

        // Act
        options
            .AddDocument("v1")
            .AddArazzoDocument("v1", "Workflows", "/workflows/{documentName}.yaml", isDefault: true);

        // Assert
        var documents = GetDocuments(options);
        documents.Count.ShouldBe(2);
        var arazzo = documents[1];
        arazzo.Title.ShouldBe("Workflows");
        arazzo.RoutePattern.ShouldBe("/workflows/{documentName}.yaml");
        arazzo.IsDefault.ShouldBeTrue();
    }

    [Theory]
    [InlineData("", "/arazzo/{documentName}.json")]
    [InlineData("v1", "")]
    public void AddArazzoDocument_BlankNameOrRoute_Throws(string documentName, string routePattern)
    {
        // Act & Assert
        Should.Throw<ArgumentException>(() => new ScalarOptions().AddArazzoDocument(documentName, routePattern: routePattern));
        Should.Throw<ArgumentNullException>(() => ScalarArazzoAspireExtensions.AddArazzoDocument<ScalarOptions>(null!));
    }
}
