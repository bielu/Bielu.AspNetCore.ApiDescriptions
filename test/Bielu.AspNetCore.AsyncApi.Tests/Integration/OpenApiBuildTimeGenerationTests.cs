using System.Reflection;
using System.Text.Json.Nodes;
using Bielu.AspNetCore.AsyncApi.Extensions;
using Bielu.AspNetCore.AsyncApi.Services;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.ApiDescriptions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.OpenApi;
using Shouldly;
using Xunit;

namespace Bielu.AspNetCore.AsyncApi.Tests.Integration;

/// <summary>
/// Build-time generation (<c>dotnet getdocument</c>) in an app that calls both <c>AddOpenApi()</c> and
/// <c>AddAsyncApi()</c>. Both libraries declare a <c>Microsoft.Extensions.ApiDescriptions.IDocumentProvider</c>
/// and the tool asks only the first type it finds, so whichever it finds must list and write both documents,
/// under names that don't map to the same output file.
/// </summary>
public class OpenApiBuildTimeGenerationTests
{
    private const string OpenApiDocumentProviderInterface =
        "Microsoft.Extensions.ApiDescriptions.IDocumentProvider, Microsoft.AspNetCore.OpenApi";

    private static async Task<WebApplication> CreateAppAsync(bool openApiFirst = true, Action<AsyncApiOptions>? configureAsyncApi = null)
    {
        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseTestServer();
        if (openApiFirst)
        {
            builder.Services.AddOpenApi();
        }

        builder.Services.AddAsyncApi(configureAsyncApi ?? (_ => { }));
        if (!openApiFirst)
        {
            builder.Services.AddOpenApi();
        }

        var app = builder.Build();
        app.MapGet("/hello", () => "world");
        await app.StartAsync();
        return app;
    }

    private static async Task<JsonNode> GenerateAsync(IDocumentProvider provider, string documentName)
    {
        using var writer = new StringWriter();
        await provider.GenerateAsync(documentName, writer);
        return JsonNode.Parse(writer.ToString())!;
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task GetDocumentNames_WithOpenApi_ListsBothDocumentsUnderDistinctNames(bool openApiFirst)
    {
        // Arrange
        await using var app = await CreateAppAsync(openApiFirst);
        var provider = app.Services.GetRequiredService<IDocumentProvider>();

        // Act
        var names = provider.GetDocumentNames().ToList();

        // Assert
        names.ShouldBe(["v1", "asyncapi-v1"], ignoreOrder: true);
    }

    [Fact]
    public async Task GenerateAsync_WithOpenApi_WritesEachDocumentFromItsOwnLibrary()
    {
        // Arrange
        await using var app = await CreateAppAsync();
        var provider = app.Services.GetRequiredService<IDocumentProvider>();

        // Act
        var openApi = await GenerateAsync(provider, "v1");
        var asyncApi = await GenerateAsync(provider, "asyncapi-v1");

        // Assert
        openApi["openapi"].ShouldNotBeNull();
        openApi["paths"]!["/hello"].ShouldNotBeNull();
        asyncApi["asyncapi"]!.GetValue<string>().ShouldBe("3.1.0");
    }

    [Fact]
    public async Task GenerateAsync_WithBuildTimeDocumentName_UsesConfiguredName()
    {
        // Arrange
        await using var app = await CreateAppAsync(configureAsyncApi: options => options.BuildTimeDocumentName = "events");
        var provider = app.Services.GetRequiredService<IDocumentProvider>();

        // Act
        var names = provider.GetDocumentNames().ToList();
        var asyncApi = await GenerateAsync(provider, "events");

        // Assert
        names.ShouldBe(["v1", "events"], ignoreOrder: true);
        asyncApi["asyncapi"].ShouldNotBeNull();
    }

    [Fact]
    public async Task GetDocumentNames_BuildTimeDocumentNameMatchingOpenApiDocument_Throws()
    {
        // Arrange
        await using var app = await CreateAppAsync(configureAsyncApi: options => options.BuildTimeDocumentName = "v1");
        var provider = app.Services.GetRequiredService<IDocumentProvider>();

        // Act
        var exception = Should.Throw<InvalidOperationException>(() => provider.GetDocumentNames().ToList());

        // Assert
        exception.Message.ShouldContain("'v1'");
        exception.Message.ShouldContain("OpenAPI document");
    }

    [Fact]
    public async Task GetDocumentNames_TwoAsyncApiDocumentsWithSameBuildTimeName_Throws()
    {
        // Arrange
        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseTestServer();
        builder.Services.AddAsyncApi("orders", options => options.BuildTimeDocumentName = "events");
        builder.Services.AddAsyncApi("payments", options => options.BuildTimeDocumentName = "events");
        await using var app = builder.Build();
        await app.StartAsync();
        var provider = app.Services.GetRequiredService<IDocumentProvider>();

        // Act
        var exception = Should.Throw<InvalidOperationException>(() => provider.GetDocumentNames().ToList());

        // Assert
        exception.Message.ShouldContain("'orders'");
        exception.Message.ShouldContain("'payments'");
        exception.Message.ShouldContain("'events'");
    }

    [Fact]
    public async Task GetDocumentNames_WithoutOpenApi_KeepsDocumentName()
    {
        // Arrange
        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseTestServer();
        builder.Services.AddAsyncApi();
        await using var app = builder.Build();
        await app.StartAsync();
        var provider = app.Services.GetRequiredService<IDocumentProvider>();

        // Act
        var names = provider.GetDocumentNames().ToList();
        var asyncApi = await GenerateAsync(provider, "v1");

        // Assert
        names.ShouldBe(["v1"]);
        asyncApi["asyncapi"].ShouldNotBeNull();
    }

    [Fact]
    public async Task OpenApiDocumentProvider_WhenRegisteredFirst_IsForwardedToAsyncApiProvider()
    {
        // Arrange - resolve OpenAPI's IDocumentProvider the way `dotnet getdocument` does when it finds that
        // type first: by name, through reflection, preferring the GenerateAsync overload taking a spec version.
        await using var app = await CreateAppAsync(openApiFirst: true);
        var serviceType = Type.GetType(OpenApiDocumentProviderInterface, throwOnError: true)!;
        var service = app.Services.GetRequiredService(serviceType);
        var getDocumentNames = serviceType.GetMethod("GetDocumentNames", Type.EmptyTypes)!;
        var generateWithVersion = serviceType.GetMethod(
            "GenerateAsync", [typeof(string), typeof(TextWriter), typeof(OpenApiSpecVersion)])!;

        async Task<JsonNode> Generate(string documentName)
        {
            using var writer = new StringWriter();
            await (Task)generateWithVersion.Invoke(service, [documentName, writer, OpenApiSpecVersion.OpenApi3_0])!;
            return JsonNode.Parse(writer.ToString())!;
        }

        // Act
        var names = ((IEnumerable<string>)getDocumentNames.Invoke(service, null)!).ToList();
        var openApi = await Generate("v1");
        var asyncApi = await Generate("asyncapi-v1");

        // Assert
        names.ShouldBe(["v1", "asyncapi-v1"], ignoreOrder: true);
        openApi["openapi"]!.GetValue<string>().ShouldStartWith("3.0");
        asyncApi["asyncapi"]!.GetValue<string>().ShouldBe("3.1.0");
    }

    [Fact]
    public async Task OpenApiDocumentProvider_GenerateWithoutVersion_IsForwarded()
    {
        // Arrange
        await using var app = await CreateAppAsync(openApiFirst: true);
        var serviceType = Type.GetType(OpenApiDocumentProviderInterface, throwOnError: true)!;
        var service = app.Services.GetRequiredService(serviceType);
        var generate = serviceType.GetMethod("GenerateAsync", [typeof(string), typeof(TextWriter)])!;
        using var writer = new StringWriter();

        // Act
        await (Task)generate.Invoke(service, ["asyncapi-v1", writer])!;

        // Assert
        JsonNode.Parse(writer.ToString())!["asyncapi"].ShouldNotBeNull();
    }
}
