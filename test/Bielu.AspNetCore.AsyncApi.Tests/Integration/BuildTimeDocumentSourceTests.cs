using Bielu.AspNetCore.ApiDescriptions;
using Bielu.AspNetCore.AsyncApi.Extensions;
using Bielu.AspNetCore.AsyncApi.Services;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.ApiDescriptions;
using Microsoft.Extensions.DependencyInjection;
using Shouldly;
using Xunit;

namespace Bielu.AspNetCore.AsyncApi.Tests.Integration;

/// <summary>
/// Other document families (such as Arazzo) join build-time generation through <see cref="IBuildTimeDocumentSource"/>.
/// Sources ordered after AsyncAPI must never rename an AsyncAPI or OpenAPI document, and the result must not depend
/// on the order anything was registered in.
/// </summary>
public class BuildTimeDocumentSourceTests
{
    private sealed class FakeSource(string prefix, params BuildTimeDocument[] documents) : IBuildTimeDocumentSource
    {
        public string Kind => "Fake";

        public string NamePrefix => prefix;

        public int Order => 100;

        public IEnumerable<BuildTimeDocument> GetDocuments() => documents;

        public Task GenerateAsync(string documentName, TextWriter writer) =>
            writer.WriteAsync($"{prefix}{documentName}");
    }

    private static async Task<WebApplication> CreateAppAsync(
        IReadOnlyList<IBuildTimeDocumentSource> sources,
        bool withOpenApi = false,
        string asyncApiDocumentName = "v1",
        Action<AsyncApiOptions>? configureAsyncApi = null)
    {
        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseTestServer();
        if (withOpenApi)
        {
            builder.Services.AddOpenApi();
        }

        foreach (var source in sources)
        {
            builder.Services.AddSingleton(source);
        }

        builder.Services.AddAsyncApi(asyncApiDocumentName, configureAsyncApi ?? (_ => { }));

        var app = builder.Build();
        await app.StartAsync();
        return app;
    }

    private static async Task<string> GenerateAsync(IDocumentProvider provider, string documentName)
    {
        using var writer = new StringWriter();
        await provider.GenerateAsync(documentName, writer);
        return writer.ToString();
    }

    [Fact]
    public async Task GetDocumentNames_SourceDocumentWithFreeName_KeepsItsName()
    {
        // Arrange
        await using var app = await CreateAppAsync([new FakeSource("arazzo-", new BuildTimeDocument("workflows"))]);

        // Act
        var names = app.Services.GetRequiredService<IDocumentProvider>().GetDocumentNames().ToList();

        // Assert
        names.ShouldBe(["v1", "workflows"], ignoreOrder: true);
    }

    [Fact]
    public async Task GetDocumentNames_SourceDocumentNamedLikeAsyncApiDocument_GetsPrefixAndLeavesAsyncApiName()
    {
        // Arrange
        await using var app = await CreateAppAsync([new FakeSource("arazzo-", new BuildTimeDocument("v1"))]);

        // Act
        var names = app.Services.GetRequiredService<IDocumentProvider>().GetDocumentNames().ToList();

        // Assert
        names.ShouldBe(["v1", "arazzo-v1"], ignoreOrder: true);
    }

    [Fact]
    public async Task GetDocumentNames_SourceAndOpenApiAndAsyncApiShareName_ListsThreeDistinctNames()
    {
        // Arrange
        await using var app = await CreateAppAsync([new FakeSource("arazzo-", new BuildTimeDocument("v1"))], withOpenApi: true);

        // Act
        var names = app.Services.GetRequiredService<IDocumentProvider>().GetDocumentNames().ToList();

        // Assert
        names.ShouldBe(["v1", "asyncapi-v1", "arazzo-v1"], ignoreOrder: true);
    }

    [Fact]
    public async Task GetDocumentNames_ConfiguredBuildTimeName_IsUsed()
    {
        // Arrange
        await using var app = await CreateAppAsync([new FakeSource("arazzo-", new BuildTimeDocument("v1", "flows"))]);

        // Act
        var names = app.Services.GetRequiredService<IDocumentProvider>().GetDocumentNames().ToList();

        // Assert
        names.ShouldBe(["v1", "flows"], ignoreOrder: true);
    }

    [Fact]
    public async Task GetDocumentNames_ConfiguredNameTakenByAsyncApi_Throws()
    {
        // Arrange
        await using var app = await CreateAppAsync([new FakeSource("arazzo-", new BuildTimeDocument("workflows", "v1"))]);
        var provider = app.Services.GetRequiredService<IDocumentProvider>();

        // Act & Assert
        Should.Throw<InvalidOperationException>(() => provider.GetDocumentNames().ToList())
            .Message.ShouldContain("'v1'");
    }

    [Fact]
    public async Task GetDocumentNames_TwoSourcesShareName_BothGetTheirPrefix()
    {
        // Arrange
        await using var app = await CreateAppAsync(
        [
            new FakeSource("arazzo-", new BuildTimeDocument("shared")),
            new FakeSource("other-", new BuildTimeDocument("shared")),
        ]);

        // Act
        var names = app.Services.GetRequiredService<IDocumentProvider>().GetDocumentNames().ToList();

        // Assert
        names.ShouldBe(["v1", "arazzo-shared", "other-shared"], ignoreOrder: true);
    }

    [Fact]
    public async Task GetDocumentNames_SourceRegistrationOrderReversed_GivesSameNames()
    {
        // Arrange
        var first = new FakeSource("arazzo-", new BuildTimeDocument("v1"), new BuildTimeDocument("shared"));
        var second = new FakeSource("other-", new BuildTimeDocument("shared"));
        await using var forward = await CreateAppAsync([first, second], withOpenApi: true);
        await using var reversed = await CreateAppAsync([second, first], withOpenApi: true);

        // Act
        var forwardNames = forward.Services.GetRequiredService<IDocumentProvider>().GetDocumentNames().Order().ToList();
        var reversedNames = reversed.Services.GetRequiredService<IDocumentProvider>().GetDocumentNames().Order().ToList();

        // Assert
        reversedNames.ShouldBe(forwardNames);
    }

    [Fact]
    public async Task GenerateAsync_PrefixedSourceDocument_RoutesToSourceWithItsOwnName()
    {
        // Arrange
        await using var app = await CreateAppAsync([new FakeSource("arazzo-", new BuildTimeDocument("v1"))]);
        var provider = app.Services.GetRequiredService<IDocumentProvider>();

        // Act
        var written = await GenerateAsync(provider, "arazzo-v1");

        // Assert
        written.ShouldBe("arazzo-v1");
    }

    [Fact]
    public async Task GenerateAsync_AsyncApiDocumentNextToSource_StillWritesAsyncApiDocument()
    {
        // Arrange
        await using var app = await CreateAppAsync([new FakeSource("arazzo-", new BuildTimeDocument("v1"))]);
        var provider = app.Services.GetRequiredService<IDocumentProvider>();

        // Act
        var written = await GenerateAsync(provider, "v1");

        // Assert
        written.ShouldContain("\"asyncapi\"");
    }

    [Fact]
    public async Task GetDocumentNames_NoSources_IsUnchanged()
    {
        // Arrange
        await using var app = await CreateAppAsync([], withOpenApi: true);

        // Act
        var names = app.Services.GetRequiredService<IDocumentProvider>().GetDocumentNames().ToList();

        // Assert
        names.ShouldBe(["v1", "asyncapi-v1"], ignoreOrder: true);
    }
}
