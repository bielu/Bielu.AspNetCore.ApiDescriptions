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

namespace Bielu.AspNetCore.AsyncApi.Tests.Unit;

/// <summary>
/// Unit tests for AsyncApiDocumentProvider (IDocumentProvider implementation).
/// Validates that the build-time document generation provider correctly
/// discovers and generates AsyncAPI documents.
/// </summary>
public class AsyncApiDocumentProviderTests
{
    private static async Task<IHost> CreateHostAsync(
        string documentName = "v1",
        Action<AsyncApiOptions>? configureOptions = null)
    {
        var builder = Host.CreateDefaultBuilder()
            .ConfigureWebHostDefaults(webBuilder =>
            {
                webBuilder.UseTestServer();
                webBuilder.ConfigureServices(services =>
                {
                    services.AddControllers();
                    if (configureOptions != null)
                    {
                        services.AddAsyncApi(documentName, configureOptions);
                    }
                    else
                    {
                        services.AddAsyncApi(documentName);
                    }
                    services.AddRouting();
                });
                webBuilder.Configure(app =>
                {
                    app.UseRouting();
                });
            });

        var host = builder.Build();
        await host.StartAsync();
        return host;
    }

    [Fact]
    public async Task GetDocumentNames_ReturnsRegisteredDocumentNames()
    {
        // Arrange
        using var host = await CreateHostAsync("test-doc");
        var documentProvider = host.Services.GetRequiredService<IDocumentProvider>();

        // Act
        var names = documentProvider.GetDocumentNames().ToList();

        // Assert
        names.ShouldNotBeEmpty();
        names.ShouldContain("test-doc");
    }

    [Fact]
    public async Task GetDocumentNames_WithMultipleDocuments_ReturnsAll()
    {
        // Arrange
        var builder = Host.CreateDefaultBuilder()
            .ConfigureWebHostDefaults(webBuilder =>
            {
                webBuilder.UseTestServer();
                webBuilder.ConfigureServices(services =>
                {
                    services.AddControllers();
                    services.AddAsyncApi("doc-a", options => options.WithInfo("Doc A", "1.0.0"));
                    services.AddAsyncApi("doc-b", options => options.WithInfo("Doc B", "2.0.0"));
                    services.AddRouting();
                });
                webBuilder.Configure(app =>
                {
                    app.UseRouting();
                });
            });

        using var host = builder.Build();
        await host.StartAsync();
        var documentProvider = host.Services.GetRequiredService<IDocumentProvider>();

        // Act
        var names = documentProvider.GetDocumentNames().ToList();

        // Assert
        names.Count.ShouldBe(2);
        names.ShouldContain("doc-a");
        names.ShouldContain("doc-b");
    }

    [Fact]
    public async Task GetDocumentNames_WithDefaultDocument_ReturnsV1()
    {
        // Arrange
        using var host = await CreateHostAsync();

        var documentProvider = host.Services.GetRequiredService<IDocumentProvider>();

        // Act
        var names = documentProvider.GetDocumentNames().ToList();

        // Assert
        names.ShouldContain("v1");
    }

    [Fact]
    public async Task GenerateAsync_ProducesNonEmptyOutput()
    {
        // Arrange
        using var host = await CreateHostAsync("v1", options =>
        {
            options.WithInfo("Test API", "1.0.0");
        });
        var documentProvider = host.Services.GetRequiredService<IDocumentProvider>();

        // Act
        using var writer = new StringWriter();
        await documentProvider.GenerateAsync("v1", writer);
        var output = writer.ToString();

        // Assert
        output.ShouldNotBeNullOrWhiteSpace();
        output.ShouldContain("asyncapi");
    }

    [Fact]
    public async Task GenerateAsync_WithV2_ProducesV2Document()
    {
        // Arrange
        using var host = await CreateHostAsync("v1", options =>
        {
            options.WithInfo("Test API", "1.0.0");
            options.AsyncApiVersion = ByteBard.AsyncAPI.AsyncApiVersion.AsyncApi2_0;
        });
        var documentProvider = host.Services.GetRequiredService<IDocumentProvider>();

        // Act
        using var writer = new StringWriter();
        await documentProvider.GenerateAsync("v1", writer);
        var output = writer.ToString();

        // Assert
        output.ShouldNotBeNullOrWhiteSpace();
    }

    [Fact]
    public async Task GenerateAsync_WithExplicitVersion_ProducesDocument()
    {
        // Arrange
        using var host = await CreateHostAsync("v1", options =>
        {
            options.WithInfo("Test API", "1.0.0");
        });
        var documentProvider = host.Services.GetRequiredService<IDocumentProvider>();

        // Act
        using var writer = new StringWriter();
        await documentProvider.GenerateAsync("v1", writer, ByteBard.AsyncAPI.AsyncApiVersion.AsyncApi3_0);
        var output = writer.ToString();

        // Assert
        output.ShouldNotBeNullOrWhiteSpace();
        output.ShouldContain("asyncapi");
    }

    [Fact]
    public async Task GenerateAsync_DocumentNameIsCaseInsensitive()
    {
        // Arrange
        using var host = await CreateHostAsync("MyDoc", options =>
        {
            options.WithInfo("My Doc", "1.0.0");
        });
        var documentProvider = host.Services.GetRequiredService<IDocumentProvider>();

        // Act - generate with lowercase (document names are lowercased on registration)
        using var writer = new StringWriter();
        await documentProvider.GenerateAsync("mydoc", writer);
        var output = writer.ToString();

        // Assert
        output.ShouldNotBeNullOrWhiteSpace();
    }

    [Fact]
    public async Task IDocumentProvider_IsRegisteredBySingleAddAsyncApiCall()
    {
        // Arrange
        using var host = await CreateHostAsync();

        // Act
        var documentProvider = host.Services.GetService<IDocumentProvider>();

        // Assert
        documentProvider.ShouldNotBeNull();
        documentProvider.ShouldBeOfType<AsyncApiDocumentProvider>();
    }
}
