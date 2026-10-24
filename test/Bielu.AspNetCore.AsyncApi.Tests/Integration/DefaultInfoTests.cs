using Bielu.AspNetCore.AsyncApi.Extensions;
using Bielu.AspNetCore.AsyncApi.Services;
using ByteBard.AsyncAPI.Models;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Shouldly;
using Xunit;

namespace Bielu.AspNetCore.AsyncApi.Tests.Integration;

/// <summary>
/// A document whose options don't set <c>info.version</c> gets <see cref="AsyncApiGeneratorConstants.DefaultInfoVersion"/>.
/// </summary>
public class DefaultInfoTests
{
    private const string DocumentName = "default-info";

    [Fact]
    public async Task GetAsyncApiDocument_NoInfoConfigured_UsesDefaultInfoVersion()
    {
        // Act
        var document = await GetDocumentAsync(_ => { });

        // Assert
        document.Info.Version.ShouldBe(AsyncApiGeneratorConstants.DefaultInfoVersion);
    }

    [Fact]
    public async Task GetAsyncApiDocument_InfoWithoutVersion_UsesDefaultInfoVersion()
    {
        // Act
        var document = await GetDocumentAsync(options => options.Info = new AsyncApiInfo { Title = "Lights" });

        // Assert
        document.Info.Title.ShouldBe("Lights");
        document.Info.Version.ShouldBe(AsyncApiGeneratorConstants.DefaultInfoVersion);
    }

    [Fact]
    public async Task GetAsyncApiDocument_InfoWithVersion_UsesConfiguredVersion()
    {
        // Act
        var document = await GetDocumentAsync(options => options.WithInfo("Lights", "2.3.0"));

        // Assert
        document.Info.Version.ShouldBe("2.3.0");
    }

    private static async Task<AsyncApiDocument> GetDocumentAsync(Action<AsyncApiOptions> configure)
    {
        // Arrange
        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseTestServer();
        builder.Services.AddAsyncApi(DocumentName, configure);

        await using var app = builder.Build();
        await app.StartAsync();

        var document = await app.Services.GetRequiredKeyedService<IAsyncApiDocumentProvider>(DocumentName)
            .GetAsyncApiDocumentAsync();
        await app.StopAsync();

        return document;
    }
}
