using System.Net;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Scalar.AspNetCore;
using Shouldly;
using Xunit;

namespace Bielu.AspNetCore.Arazzo.Scalar.Tests.Integration;

public class ScalarArazzoAssetsEndpointTests
{
    private static async Task<WebApplication> StartAsync(Action<WebApplication> map)
    {
        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseTestServer();
        var app = builder.Build();
        map(app);
        await app.StartAsync();
        return app;
    }

    [Fact]
    public async Task MapScalarArazzoAssets_ServesTheEmbeddedBundle()
    {
        // Arrange
        await using var app = await StartAsync(endpoints => endpoints.MapScalarArazzoAssets());
        var client = app.GetTestClient();

        // Act
        var response = await client.GetAsync("/bielu/scalar/arazzo/plugin.js");

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        response.Content.Headers.ContentType!.MediaType.ShouldBe("text/javascript");
        var body = await response.Content.ReadAsStringAsync();
        // The bundle registers the document element; its tag is a stable marker.
        body.ShouldContain("bielu-arazzo-document");
    }

    [Fact]
    public async Task MapScalarArazzoAssets_SendsAnETagAndRevalidation()
    {
        // Arrange
        await using var app = await StartAsync(endpoints => endpoints.MapScalarArazzoAssets());
        var client = app.GetTestClient();

        // Act
        var first = await client.GetAsync("/bielu/scalar/arazzo/plugin.js");
        var second = await client.GetAsync("/bielu/scalar/arazzo/plugin.js");

        // Assert
        first.Headers.CacheControl!.NoCache.ShouldBeTrue();
        first.Headers.ETag.ShouldNotBeNull();
        first.Headers.ETag!.IsWeak.ShouldBeFalse();
        // Derived from the content, so it is the same on every response.
        second.Headers.ETag.ShouldBe(first.Headers.ETag);
        first.Content.Headers.ContentLength.ShouldBe((await first.Content.ReadAsByteArrayAsync()).Length);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task MapScalarArazzoAssets_MatchingIfNoneMatch_Returns304WithoutBody(bool wildcard)
    {
        // Arrange
        await using var app = await StartAsync(endpoints => endpoints.MapScalarArazzoAssets());
        var client = app.GetTestClient();
        var etag = (await client.GetAsync("/bielu/scalar/arazzo/plugin.js")).Headers.ETag!;
        using var request = new HttpRequestMessage(HttpMethod.Get, "/bielu/scalar/arazzo/plugin.js");
        request.Headers.TryAddWithoutValidation("If-None-Match", wildcard ? "*" : $"\"other\", {etag.Tag}");

        // Act
        var response = await client.SendAsync(request);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.NotModified);
        response.Headers.ETag.ShouldBe(etag);
        (await response.Content.ReadAsByteArrayAsync()).ShouldBeEmpty();
    }

    [Fact]
    public async Task MapScalarArazzoAssets_StaleIfNoneMatch_ServesTheBundle()
    {
        // Arrange
        await using var app = await StartAsync(endpoints => endpoints.MapScalarArazzoAssets());
        var client = app.GetTestClient();
        using var request = new HttpRequestMessage(HttpMethod.Get, "/bielu/scalar/arazzo/plugin.js");
        request.Headers.TryAddWithoutValidation("If-None-Match", "\"an-older-bundle\"");

        // Act
        var response = await client.SendAsync(request);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        (await response.Content.ReadAsStringAsync()).ShouldContain("bielu-arazzo-document");
    }

    [Fact]
    public async Task MapScalarArazzoAssets_CustomPath_ServesTheBundleThere()
    {
        // Arrange
        await using var app = await StartAsync(endpoints => endpoints.MapScalarArazzoAssets("/docs/arazzo/"));
        var client = app.GetTestClient();

        // Act
        var response = await client.GetAsync("/docs/arazzo/plugin.js");

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    [Fact]
    public async Task ScalarPage_ListsTheArazzoDocumentAndLoadsThePlugin()
    {
        // Arrange
        await using var app = await StartAsync(endpoints => endpoints.MapScalarApiReference(options => options
            .AddArazzoDocument("v1", "Workflows")
            .WithArazzo()));
        var client = app.GetTestClient();

        // Act
        var html = await client.GetStringAsync("/scalar/");

        // Assert
        // Scalar writes source URLs relative to the app base and makes them absolute in the browser.
        html.ShouldContain("\"url\":\"arazzo/v1.json\"");
        html.ShouldContain("Workflows");
        html.ShouldContain("/bielu/scalar/arazzo/plugin.js");
    }

    [Fact]
    public async Task ScalarPage_UsesACustomArazzoRoute()
    {
        // Arrange
        await using var app = await StartAsync(endpoints => endpoints.MapScalarApiReference(options => options
            .AddArazzoDocument("orders", routePattern: "/workflows/{documentName}.yaml", isDefault: true)));
        var client = app.GetTestClient();

        // Act
        var html = await client.GetStringAsync("/scalar/");

        // Assert
        html.ShouldContain("\"url\":\"workflows/orders.yaml\"");
    }

    [Fact]
    public void MapScalarArazzoAssets_ThrowsForNullEndpoints()
    {
        // Arrange
        Microsoft.AspNetCore.Routing.IEndpointRouteBuilder endpoints = null!;

        // Act / Assert
        Should.Throw<ArgumentNullException>(() => endpoints.MapScalarArazzoAssets());
    }
}
