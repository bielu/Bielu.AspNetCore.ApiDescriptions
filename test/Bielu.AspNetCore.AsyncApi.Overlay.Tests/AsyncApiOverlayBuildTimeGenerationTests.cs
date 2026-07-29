using System.Text.Json;
using Bielu.AspNetCore.AsyncApi.Extensions;
using Bielu.AspNetCore.AsyncApi.Services;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.ApiDescriptions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Shouldly;
using Xunit;

namespace Bielu.AspNetCore.AsyncApi.Overlay.Tests;

/// <summary>
/// Build-time generation must emit the same bytes the endpoint serves, otherwise a checked-in document
/// and the live one would disagree about whether the overlay had been applied.
/// </summary>
public class AsyncApiOverlayBuildTimeGenerationTests
{
    [Fact]
    public async Task DocumentProvider_AppliesRegisteredOverlays()
    {
        // Arrange
        using var host = await CreateTestHostAsync(options => options.AddOverlay(TestOverlays.RetitleDocument()));
        var documentProvider = host.Services.GetRequiredService<IDocumentProvider>();
        await using var writer = new StringWriter();

        // Act
        await documentProvider.GenerateAsync(AsyncApiGeneratorConstants.DefaultDocumentName, writer);

        // Assert
        using var document = JsonDocument.Parse(writer.ToString());
        document.RootElement.GetProperty("info").GetProperty("title").GetString().ShouldBe("In-Memory Overlay");
    }

    [Fact]
    public async Task DocumentProvider_WithoutOverlays_StillProducesAValidV3Document()
    {
        // Arrange — guards the refactor that routed the V3 path through the serialization helper so it
        // could be buffered and transformed, instead of writing straight to the TextWriter.
        using var host = await CreateTestHostAsync();
        var documentProvider = host.Services.GetRequiredService<IDocumentProvider>();
        await using var writer = new StringWriter();

        // Act
        await documentProvider.GenerateAsync(AsyncApiGeneratorConstants.DefaultDocumentName, writer);

        // Assert
        using var document = JsonDocument.Parse(writer.ToString());
        document.RootElement.GetProperty("asyncapi").GetString().ShouldStartWith("3.");
        document.RootElement.GetProperty("info").GetProperty("title").GetString().ShouldBe("Test API");
    }

    private static async Task<IHost> CreateTestHostAsync(Action<AsyncApiOptions>? configureOptions = null)
    {
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
                webBuilder.Configure(app => app.UseRouting());
            });

        var host = builder.Build();
        await host.StartAsync();
        return host;
    }
}
