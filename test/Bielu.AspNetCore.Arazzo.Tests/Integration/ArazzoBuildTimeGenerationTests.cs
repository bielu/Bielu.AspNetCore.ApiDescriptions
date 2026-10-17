using System.Text.Json;
using Bielu.Arazzo;
using Bielu.AspNetCore.Arazzo.Extensions;
using Bielu.AspNetCore.Arazzo.Services;
using Bielu.AspNetCore.AsyncApi.Extensions;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.ApiDescriptions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Shouldly;
using Xunit;

namespace Bielu.AspNetCore.Arazzo.Tests.Integration;

/// <summary>
/// Build-time generation (<c>dotnet getdocument</c>) must list and write Arazzo documents next to the AsyncAPI and
/// OpenAPI ones, under names that never map to the same file, whatever order the registrations were made in.
/// </summary>
public class ArazzoBuildTimeGenerationTests
{
    public enum Registration
    {
        OpenApi,
        AsyncApi,
        Arazzo,
    }

    private static async Task<WebApplication> CreateAppAsync(
        Registration[] order, Action<ArazzoOptions>? configureArazzo = null, string arazzoName = "v1")
    {
        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseTestServer();
        // Off by default outside Development; on here so resolving a scoped service from the root provider fails.
        builder.Host.UseDefaultServiceProvider(o => o.ValidateScopes = true);
        builder.Services.AddScoped<ScopedDependency>();

        foreach (var registration in order)
        {
            switch (registration)
            {
                case Registration.OpenApi:
                    builder.Services.AddOpenApi();
                    break;
                case Registration.AsyncApi:
                    builder.Services.AddAsyncApi();
                    break;
                case Registration.Arazzo:
                    builder.Services.AddArazzo(arazzoName, options =>
                    {
                        options.WithInfo("Test Workflows", "1.0.0");
                        options.ValidateSourceReferencesOnStartup = false;
                        options.AddWorkflow("noop", wf => wf.Step("only", s => s.Workflow("noop")));
                        configureArazzo?.Invoke(options);
                    });
                    break;
            }
        }

        var app = builder.Build();
        app.MapGet("/hello", () => "world");
        await app.StartAsync();
        return app;
    }

    private static async Task<string> GenerateAsync(WebApplication app, string documentName)
    {
        using var writer = new StringWriter();
        await app.Services.GetRequiredService<IDocumentProvider>().GenerateAsync(documentName, writer);
        return writer.ToString();
    }

    private static List<string> Names(WebApplication app) =>
        app.Services.GetRequiredService<IDocumentProvider>().GetDocumentNames().Order().ToList();

    [Fact]
    public async Task GetDocumentNames_ArazzoOnly_ListsTheArazzoDocument()
    {
        // Arrange
        await using var app = await CreateAppAsync([Registration.Arazzo]);

        // Act
        var names = Names(app);

        // Assert
        names.ShouldBe(["v1"]);
    }

    [Fact]
    public async Task GenerateAsync_ArazzoOnly_WritesTheDocumentTheEndpointWouldServe()
    {
        // Arrange
        await using var app = await CreateAppAsync([Registration.Arazzo]);

        // Act
        var json = await GenerateAsync(app, "v1");

        // Assert
        using var document = JsonDocument.Parse(json);
        document.RootElement.GetProperty("arazzo").GetString().ShouldBe("1.1.0");
        document.RootElement.GetProperty("info").GetProperty("title").GetString().ShouldBe("Test Workflows");
    }

    [Fact]
    public async Task GenerateAsync_VersionSet_DeclaresThatVersion()
    {
        // Arrange
        await using var app = await CreateAppAsync([Registration.Arazzo], o => o.Version = ArazzoVersion.V1_0);

        // Act
        var json = await GenerateAsync(app, "v1");

        // Assert
        using var document = JsonDocument.Parse(json);
        document.RootElement.GetProperty("arazzo").GetString().ShouldBe("1.0.0");
    }

    [Fact]
    public async Task GenerateAsync_SerializedTransformerRegistered_AppliesIt()
    {
        // Arrange
        await using var app = await CreateAppAsync([Registration.Arazzo], o =>
            o.AddSerializedDocumentTransformer((json, _, _) => ValueTask.FromResult(json.Replace("Test Workflows", "Transformed"))));

        // Act
        var json = await GenerateAsync(app, "v1");

        // Assert
        json.ShouldContain("Transformed");
        json.ShouldNotContain("Test Workflows");
    }

    [Fact]
    public async Task GenerateAsync_TransformerResolvesScopedService_ResolvesItFromAScope()
    {
        // Arrange
        await using var app = await CreateAppAsync([Registration.Arazzo], o =>
            o.AddSerializedDocumentTransformer((json, context, _) =>
            {
                context.ApplicationServices.GetRequiredService<ScopedDependency>();
                return ValueTask.FromResult(json);
            }));

        // Act
        var json = await GenerateAsync(app, "v1");

        // Assert
        json.ShouldContain("Test Workflows");
    }

    [Fact]
    public async Task GenerateAsync_TransformerReturnsEmpty_Throws()
    {
        // Arrange
        await using var app = await CreateAppAsync([Registration.Arazzo], o =>
            o.AddSerializedDocumentTransformer((_, _, _) => ValueTask.FromResult(" ")));

        // Act
        var act = () => GenerateAsync(app, "v1");

        // Assert
        var exception = await act.ShouldThrowAsync<InvalidOperationException>();
        exception.Message.ShouldContain("'v1' serialized to an empty document");
    }

    [Fact]
    public async Task GetDocumentNames_AsyncApiAndArazzoShareName_PrefixesOnlyArazzo()
    {
        // Arrange
        await using var app = await CreateAppAsync([Registration.AsyncApi, Registration.Arazzo]);

        // Act
        var names = Names(app);

        // Assert
        names.ShouldBe(["arazzo-v1", "v1"]);
    }

    [Fact]
    public async Task GenerateAsync_PrefixedName_WritesArazzoAndUnprefixedWritesAsyncApi()
    {
        // Arrange
        await using var app = await CreateAppAsync([Registration.AsyncApi, Registration.Arazzo]);

        // Act
        var arazzo = await GenerateAsync(app, "arazzo-v1");
        var asyncApi = await GenerateAsync(app, "v1");

        // Assert
        arazzo.ShouldContain("\"arazzo\"");
        asyncApi.ShouldContain("\"asyncapi\"");
    }

    [Fact]
    public async Task GetDocumentNames_OpenApiAsyncApiAndArazzoShareName_ListsThreeDistinctNames()
    {
        // Arrange
        await using var app = await CreateAppAsync([Registration.OpenApi, Registration.AsyncApi, Registration.Arazzo]);

        // Act
        var names = Names(app);

        // Assert
        names.ShouldBe(["arazzo-v1", "asyncapi-v1", "v1"]);
    }

    [Theory]
    [InlineData(Registration.AsyncApi, Registration.Arazzo)]
    [InlineData(Registration.Arazzo, Registration.AsyncApi)]
    public async Task GetDocumentNames_AsyncApiAndArazzoInEitherOrder_GiveTheSameNames(Registration first, Registration second)
    {
        // Arrange
        await using var app = await CreateAppAsync([first, second]);

        // Act
        var names = Names(app);

        // Assert
        names.ShouldBe(["arazzo-v1", "v1"]);
    }

    [Theory]
    [InlineData(Registration.OpenApi, Registration.AsyncApi, Registration.Arazzo)]
    [InlineData(Registration.OpenApi, Registration.Arazzo, Registration.AsyncApi)]
    public async Task GetDocumentNames_OpenApiFirstThenOthersInEitherOrder_GiveTheSameNames(
        Registration first, Registration second, Registration third)
    {
        // Arrange
        await using var app = await CreateAppAsync([first, second, third]);

        // Act
        var names = Names(app);

        // Assert
        names.ShouldBe(["arazzo-v1", "asyncapi-v1", "v1"]);
    }

    [Fact]
    public async Task GetDocumentNames_BuildTimeDocumentNameSet_IsUsed()
    {
        // Arrange
        await using var app = await CreateAppAsync(
            [Registration.AsyncApi, Registration.Arazzo], o => o.BuildTimeDocumentName = "workflows");

        // Act
        var names = Names(app);

        // Assert
        names.ShouldBe(["v1", "workflows"]);
    }

    [Fact]
    public async Task GetDocumentNames_BuildTimeDocumentNameTakenByAsyncApi_Throws()
    {
        // Arrange
        await using var app = await CreateAppAsync(
            [Registration.AsyncApi, Registration.Arazzo], o => o.BuildTimeDocumentName = "v1");
        var provider = app.Services.GetRequiredService<IDocumentProvider>();

        // Act & Assert
        Should.Throw<InvalidOperationException>(() => provider.GetDocumentNames().ToList());
    }

    [Fact]
    public async Task GetDocumentNames_NoArazzoRegistered_IsUnchanged()
    {
        // Arrange
        await using var app = await CreateAppAsync([Registration.OpenApi, Registration.AsyncApi]);

        // Act
        var names = Names(app);

        // Assert
        names.ShouldBe(["asyncapi-v1", "v1"]);
    }

    private sealed class ScopedDependency;
}
