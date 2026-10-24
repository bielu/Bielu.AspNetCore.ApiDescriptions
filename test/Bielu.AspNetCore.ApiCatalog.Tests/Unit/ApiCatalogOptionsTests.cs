using Bielu.AspNetCore.ApiDescriptions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Shouldly;
using Xunit;

namespace Bielu.AspNetCore.ApiCatalog.Tests.Unit;

public class ApiCatalogOptionsTests
{
    [Fact]
    public void AddItem_AddsAnItemLink()
    {
        // Arrange
        var options = new ApiCatalogOptions();

        // Act
        var result = options.AddItem("/v2", "Version 2");

        // Assert
        result.ShouldBeSameAs(options);
        options.Items.ShouldBe([new ApiCatalogLink("/v2", null, "Version 2")]);
    }

    [Fact]
    public void AddServiceDoc_AddsAnHtmlLink()
    {
        // Arrange
        var options = new ApiCatalogOptions();

        // Act
        var result = options.AddServiceDoc("/scalar");

        // Assert
        result.ShouldBeSameAs(options);
        options.ServiceDocs.ShouldBe([new ApiCatalogLink("/scalar", "text/html", null)]);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData(" ")]
    public void AddItem_ThrowsForMissingHref(string? href)
    {
        // Act & Assert
        Should.Throw<ArgumentException>(() => new ApiCatalogOptions().AddItem(href!));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    public void AddServiceDoc_ThrowsForMissingHref(string? href)
    {
        // Act & Assert
        Should.Throw<ArgumentException>(() => new ApiCatalogOptions().AddServiceDoc(href!));
    }

    [Fact]
    public void OpenApiRoutePattern_DefaultsToMapOpenApiRoute()
    {
        // Act & Assert
        new ApiCatalogOptions().OpenApiRoutePattern.ShouldBe(ApiCatalogDefaults.DefaultOpenApiRoute);
    }

    [Fact]
    public void ApiUrl_DefaultsToAppRoot()
    {
        // Act & Assert
        new ApiCatalogOptions().ApiUrl.ShouldBe("/");
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData(" ")]
    public void ApiUrl_ThrowsForMissingValue(string? apiUrl)
    {
        // Act & Assert
        Should.Throw<ArgumentException>(() => new ApiCatalogOptions { ApiUrl = apiUrl! });
    }

    [Fact]
    public void AddApiCatalog_AppliesConfiguration()
    {
        // Arrange
        var services = new ServiceCollection();

        // Act
        services.AddApiCatalog(options => options.AddServiceDoc("/scalar"));

        // Assert
        using var provider = services.BuildServiceProvider();
        provider.GetRequiredService<IOptions<ApiCatalogOptions>>().Value.ServiceDocs.Count.ShouldBe(1);
    }

    [Fact]
    public void AddApiCatalog_WithoutConfiguration_RegistersDefaults()
    {
        // Arrange
        var services = new ServiceCollection();

        // Act
        services.AddApiCatalog();

        // Assert
        using var provider = services.BuildServiceProvider();
        provider.GetRequiredService<IOptions<ApiCatalogOptions>>().Value.Items.ShouldBeEmpty();
    }

    [Fact]
    public void AddApiCatalog_ThrowsForNullServices()
    {
        // Act & Assert
        Should.Throw<ArgumentNullException>(() => ApiCatalogServiceCollectionExtensions.AddApiCatalog(null!));
    }

    [Theory]
    [InlineData("https://example.com/api", "https://example.com/api")]
    [InlineData("/scalar", "http://localhost/base/scalar")]
    [InlineData("scalar", "http://localhost/base/scalar")]
    public void Resolve_MakesPathsAbsolute(string href, string expected)
    {
        // Act & Assert
        ApiCatalogWriter.Resolve(href, "http://localhost/base").ShouldBe(expected);
    }

    [Theory]
    [InlineData(null, "application/json")]
    [InlineData("AsyncAPI", null)]
    [InlineData(" ", "application/json")]
    public void ApiDescriptionDocumentMetadata_ThrowsForMissingValues(string? kind, string? mediaType)
    {
        // Act & Assert
        Should.Throw<ArgumentException>(() => new ApiDescriptionDocumentMetadata(kind!, mediaType!));
    }
}
