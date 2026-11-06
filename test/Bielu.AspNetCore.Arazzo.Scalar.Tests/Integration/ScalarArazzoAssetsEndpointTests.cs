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
