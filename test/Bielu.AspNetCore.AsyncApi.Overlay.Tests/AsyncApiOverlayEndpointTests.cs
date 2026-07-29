using System.Net;
using System.Text.Json;
using Bielu.AspNetCore.AsyncApi.Extensions;
using Bielu.AspNetCore.AsyncApi.Services;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Shouldly;
using Xunit;

namespace Bielu.AspNetCore.AsyncApi.Overlay.Tests;

/// <summary>
/// Integration tests proving the served document is already overlaid — the point of applying at the
/// serialization boundary rather than as a post-processing step.
/// </summary>
public class AsyncApiOverlayEndpointTests
{
    private static string GetDocumentRoute(string documentName) =>
        AsyncApiGeneratorConstants.DefaultAsyncApiRoute.Replace("{documentName}", documentName);

    [Fact]
    public async Task JsonEndpoint_WithFileOverlay_ServesOverlaidDocument()
    {
        // Arrange
        var overlayPath = TestOverlays.WriteTempOverlay(TestOverlays.RetitleYaml);
        try
        {
            using var host = await CreateTestHostAsync(options => options.AddOverlay(overlayPath));
            var client = host.GetTestClient();

            // Act
            var response = await client.GetAsync(GetDocumentRoute(AsyncApiGeneratorConstants.DefaultDocumentName));

            // Assert
            response.StatusCode.ShouldBe(HttpStatusCode.OK);
            using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
            document.RootElement.GetProperty("info").GetProperty("title").GetString().ShouldBe("Overlaid Title");
        }
        finally
        {
            File.Delete(overlayPath);
        }
    }

    [Fact]
    public async Task JsonEndpoint_WithInMemoryOverlay_ServesOverlaidDocument()
    {
        // Arrange
        using var host = await CreateTestHostAsync(options => options.AddOverlay(TestOverlays.RetitleDocument()));
        var client = host.GetTestClient();

        // Act
        var response = await client.GetAsync(GetDocumentRoute(AsyncApiGeneratorConstants.DefaultDocumentName));

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        document.RootElement.GetProperty("info").GetProperty("title").GetString().ShouldBe("In-Memory Overlay");
    }

    [Fact]
    public async Task YamlEndpoint_WithOverlay_ServesOverlaidYaml()
    {
        // Arrange
        using var host = await CreateTestHostAsync(
            options => options.AddOverlay(TestOverlays.RetitleDocument()),
            _ => "/asyncapi/{documentName}.yaml");
        var client = host.GetTestClient();

        // Act
        var response = await client.GetAsync($"/asyncapi/{AsyncApiGeneratorConstants.DefaultDocumentName}.yaml");

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        response.Content.Headers.ContentType?.MediaType.ShouldBe("text/plain+yaml");

        var body = await response.Content.ReadAsStringAsync();
        body.ShouldContain("In-Memory Overlay");
        // Re-emitted as YAML, not as the JSON tree the overlay engine worked on.
        body.TrimStart().ShouldNotStartWith("{");
    }

    [Fact]
    public async Task MultipleOverlays_ApplyInRegistrationOrder()
    {
        // Arrange
        using var host = await CreateTestHostAsync(options => options
            .AddOverlay(TestOverlays.RetitleDocument())
            .AddOverlay(ParseOverlay(TestOverlays.SecondRetitleYaml)));
        var client = host.GetTestClient();

        // Act
        var response = await client.GetAsync(GetDocumentRoute(AsyncApiGeneratorConstants.DefaultDocumentName));

        // Assert
        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        document.RootElement.GetProperty("info").GetProperty("title").GetString().ShouldBe("Second Overlay Wins");
    }

    [Fact]
    public async Task ZeroMatchTarget_WithoutStrict_ServesDocumentUnchanged()
    {
        // Arrange
        using var host = await CreateTestHostAsync(options => options.AddOverlay(ParseOverlay(TestOverlays.NoMatchYaml)));
        var client = host.GetTestClient();

        // Act
        var response = await client.GetAsync(GetDocumentRoute(AsyncApiGeneratorConstants.DefaultDocumentName));

        // Assert — the spec permits zero matches, so this is a logged warning, not a failure.
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        document.RootElement.GetProperty("info").GetProperty("title").GetString().ShouldBe("Test API");
    }

    [Fact]
    public async Task ZeroMatchTarget_WithStrict_FailsTheRequest()
    {
        // Arrange
        using var host = await CreateTestHostAsync(options => options
            .AddOverlay(ParseOverlay(TestOverlays.NoMatchYaml))
            .ConfigureOverlays(apply => apply.Strict = true));
        var client = host.GetTestClient();

        // Act
        var response = await client.GetAsync(GetDocumentRoute(AsyncApiGeneratorConstants.DefaultDocumentName));

        // Assert — an overlay that silently stops matching is exactly what strict mode exists to catch.
        response.StatusCode.ShouldBe(HttpStatusCode.InternalServerError);
    }

    [Fact]
    public async Task MissingOverlayFile_FailsTheRequestRatherThanServingUntransformed()
    {
        // Arrange
        using var host = await CreateTestHostAsync(options =>
            options.AddOverlay(Path.Combine(Path.GetTempPath(), $"absent-{Guid.NewGuid():N}.yaml")));
        var client = host.GetTestClient();

        // Act
        var response = await client.GetAsync(GetDocumentRoute(AsyncApiGeneratorConstants.DefaultDocumentName));

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.InternalServerError);
    }

    [Fact]
    public async Task NoOverlayRegistered_ServesDocumentUnchanged()
    {
        // Arrange
        using var host = await CreateTestHostAsync();
        var client = host.GetTestClient();

        // Act
        var response = await client.GetAsync(GetDocumentRoute(AsyncApiGeneratorConstants.DefaultDocumentName));

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        document.RootElement.GetProperty("info").GetProperty("title").GetString().ShouldBe("Test API");
    }

    private static Bielu.Overlay.Models.OverlayDocument ParseOverlay(string yaml)
    {
        var read = Bielu.Overlay.Readers.OverlayStringReader.Read(yaml);
        read.Document.ShouldNotBeNull();
        return read.Document!;
    }

    private static async Task<IHost> CreateTestHostAsync(
        Action<AsyncApiOptions>? configureOptions = null,
        Func<string, string>? configureEndpoint = null)
    {
        var pattern = configureEndpoint?.Invoke(AsyncApiGeneratorConstants.DefaultAsyncApiRoute)
                      ?? AsyncApiGeneratorConstants.DefaultAsyncApiRoute;

        var builder = Host.CreateDefaultBuilder()
            .ConfigureWebHostDefaults(webBuilder =>
            {
                webBuilder.UseTestServer();
                webBuilder.ConfigureServices(services =>
                {
                    services.AddControllers();
                    services.AddAsyncApi(options =>
                    {
                        options.WithInfo("Test API", "1.0.0");
                        options.AddServer("test-server", "localhost", "http");
                        configureOptions?.Invoke(options);
                    });
                    services.AddRouting();
                });
                webBuilder.Configure(app =>
                {
                    app.UseRouting();
                    app.UseEndpoints(endpoints => endpoints.MapAsyncApi(pattern));
                });
            });

        var host = builder.Build();
        await host.StartAsync();
        return host;
    }
}
