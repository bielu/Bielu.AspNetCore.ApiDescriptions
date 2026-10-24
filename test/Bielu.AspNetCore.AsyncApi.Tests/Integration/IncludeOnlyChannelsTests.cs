using Bielu.AspNetCore.AsyncApi.Attributes;
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
/// <see cref="AsyncApiOptions.IncludeOnlyChannels"/> restricts the document to the listed channels,
/// matching sanitized keys case-insensitively.
/// </summary>
public class IncludeOnlyChannelsTests
{
    private const string DocumentName = "include-only-channels";

    public sealed record LightMeasured(int Lumens);

    [AsyncApi(DocumentName)]
    public sealed class Channels
    {
        [Channel("light/measured")]
        [SendOperation(typeof(LightMeasured), OperationId = "lightMeasured")]
        public void Measured()
        {
        }

        [Channel("light/dimmed")]
        [SendOperation(typeof(LightMeasured), OperationId = "lightDimmed")]
        public void Dimmed()
        {
        }
    }

    [Fact]
    public void AddIncludedChannel_AddsSanitizedNameToCaseInsensitiveSet()
    {
        // Arrange
        var options = new AsyncApiOptions();

        // Act
        var result = options.AddIncludedChannel("light/measured");

        // Assert
        result.ShouldBe(options);
        options.IncludeOnlyChannels.Count.ShouldBe(1);
        options.IncludeOnlyChannels.Contains("LIGHTMEASURED").ShouldBeTrue();
    }

    [Fact]
    public void AddIncludedChannel_ThrowsForNullName()
    {
        // Arrange
        var options = new AsyncApiOptions();

        // Act & Assert
        Should.Throw<ArgumentNullException>(() => options.AddIncludedChannel(null!));
    }

    [Fact]
    public async Task GetAsyncApiDocument_NoIncludedChannels_IncludesEveryChannel()
    {
        // Act
        var document = await GetDocumentAsync(_ => { });

        // Assert
        document.Channels.ShouldContainKey("lightmeasured");
        document.Channels.ShouldContainKey("lightdimmed");
    }

    [Fact]
    public async Task GetAsyncApiDocument_IncludedChannel_OmitsTheOthers()
    {
        // Act
        var document = await GetDocumentAsync(options => options.AddIncludedChannel("LIGHT/MEASURED"));

        // Assert
        document.Channels.Keys.ShouldBe(["lightmeasured"]);
        document.Operations.Keys.ShouldBe(["lightMeasured"]);
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
