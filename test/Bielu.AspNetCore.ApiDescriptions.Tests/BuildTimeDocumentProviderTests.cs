using System.Text.Json.Nodes;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.ApiDescriptions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.OpenApi;
using Shouldly;
using Xunit;

namespace Bielu.AspNetCore.ApiDescriptions.Tests;

/// <summary>
/// The single provider <c>dotnet getdocument</c> queries must list every source's documents and OpenAPI's under
/// names that never collide, settled by <see cref="IBuildTimeDocumentSource.Order"/> and never by registration order.
/// </summary>
public class BuildTimeDocumentProviderTests
{
    private const string OpenApiDocumentProviderInterface =
        "Microsoft.Extensions.ApiDescriptions.IDocumentProvider, Microsoft.AspNetCore.OpenApi";

    private sealed class FakeSource(string kind, string prefix, int order, params BuildTimeDocument[] documents)
        : IBuildTimeDocumentSource
    {
        public string Kind => kind;

        public string NamePrefix => prefix;

        public int Order => order;

        public IEnumerable<BuildTimeDocument> GetDocuments() => documents;

        public Task GenerateAsync(string documentName, TextWriter writer) =>
            writer.WriteAsync($"{kind}:{documentName}");
    }

    private sealed class TypedSource : IBuildTimeDocumentSource
    {
        public string Kind => "Typed";

        public string NamePrefix => "typed-";

        public int Order => 0;

        public IEnumerable<BuildTimeDocument> GetDocuments() => [new BuildTimeDocument("typed")];

        public Task GenerateAsync(string documentName, TextWriter writer) => writer.WriteAsync(documentName);
    }

    private static async Task<WebApplication> CreateAppAsync(bool withOpenApi, params IBuildTimeDocumentSource[] sources)
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

        builder.Services.AddBuildTimeDocumentProvider();

        var app = builder.Build();
        app.MapGet("/hello", () => "world");
        await app.StartAsync();
        return app;
    }

    private static List<string> GetNames(WebApplication app) =>
        app.Services.GetRequiredService<IDocumentProvider>().GetDocumentNames().ToList();

    private static async Task<string> GenerateAsync(WebApplication app, string documentName)
    {
        using var writer = new StringWriter();
        await app.Services.GetRequiredService<IDocumentProvider>().GenerateAsync(documentName, writer);
        return writer.ToString();
    }

    [Fact]
    public async Task GetDocumentNames_NoOpenApiAndNoSources_IsEmpty()
    {
        // Arrange
        await using var app = await CreateAppAsync(withOpenApi: false);

        // Act
        var names = GetNames(app);

        // Assert
        names.ShouldBeEmpty();
    }

    [Fact]
    public async Task GetDocumentNames_HigherOrderSourceSharingName_GetsPrefixAndLeavesLowerOrderName()
    {
        // Arrange
        await using var app = await CreateAppAsync(
            withOpenApi: false,
            new FakeSource("Late", "late-", 100, new BuildTimeDocument("v1")),
            new FakeSource("Early", "early-", 0, new BuildTimeDocument("v1")));

        // Act
        var names = GetNames(app);

        // Assert
        names.ShouldBe(["v1", "late-v1"]);
    }

    [Fact]
    public async Task GetDocumentNames_SameOrderSourcesSharingName_BothGetTheirPrefix()
    {
        // Arrange
        await using var app = await CreateAppAsync(
            withOpenApi: false,
            new FakeSource("A", "a-", 0, new BuildTimeDocument("shared")),
            new FakeSource("B", "b-", 0, new BuildTimeDocument("shared")));

        // Act
        var names = GetNames(app);

        // Assert
        names.ShouldBe(["a-shared", "b-shared"]);
    }

    [Fact]
    public async Task GetDocumentNames_RegistrationOrderReversed_GivesSameNames()
    {
        // Arrange
        IBuildTimeDocumentSource early = new FakeSource("Early", "early-", 0, new BuildTimeDocument("v1"), new BuildTimeDocument("shared"));
        IBuildTimeDocumentSource late = new FakeSource("Late", "late-", 100, new BuildTimeDocument("v1"));
        IBuildTimeDocumentSource peer = new FakeSource("Peer", "peer-", 0, new BuildTimeDocument("shared"));
        await using var forward = await CreateAppAsync(withOpenApi: true, early, late, peer);
        await using var reversed = await CreateAppAsync(withOpenApi: true, peer, late, early);

        // Act
        var forwardNames = GetNames(forward);
        var reversedNames = GetNames(reversed);

        // Assert
        reversedNames.ShouldBe(forwardNames);
        forwardNames.ShouldBe(["v1", "early-v1", "early-shared", "peer-shared", "late-v1"], ignoreOrder: true);
    }

    [Fact]
    public async Task GetDocumentNames_WithOpenApi_ListsOpenApiFirstAndPrefixesSource()
    {
        // Arrange
        await using var app = await CreateAppAsync(withOpenApi: true, new FakeSource("Fake", "fake-", 0, new BuildTimeDocument("v1")));

        // Act
        var names = GetNames(app);

        // Assert
        names.ShouldBe(["v1", "fake-v1"]);
    }

    [Fact]
    public async Task GetDocumentNames_ConfiguredName_IsUsedEvenWhenFree()
    {
        // Arrange
        await using var app = await CreateAppAsync(withOpenApi: false, new FakeSource("Fake", "fake-", 0, new BuildTimeDocument("v1", "events")));

        // Act
        var names = GetNames(app);

        // Assert
        names.ShouldBe(["events"]);
    }

    [Fact]
    public async Task GetDocumentNames_ConfiguredNameTakenByOpenApi_Throws()
    {
        // Arrange
        await using var app = await CreateAppAsync(withOpenApi: true, new FakeSource("Fake", "fake-", 0, new BuildTimeDocument("events", "v1")));

        // Act
        var exception = Should.Throw<InvalidOperationException>(() => GetNames(app));

        // Assert
        exception.Message.ShouldContain("Fake document 'events'");
        exception.Message.ShouldContain("'v1'");
        exception.Message.ShouldContain("OpenAPI document");
    }

    [Fact]
    public async Task GetDocumentNames_ConfiguredNameTakenByLowerOrderSource_ThrowsNamingBoth()
    {
        // Arrange
        await using var app = await CreateAppAsync(
            withOpenApi: false,
            new FakeSource("Early", "early-", 0, new BuildTimeDocument("orders")),
            new FakeSource("Late", "late-", 100, new BuildTimeDocument("workflows", "orders")));

        // Act
        var exception = Should.Throw<InvalidOperationException>(() => GetNames(app));

        // Assert
        exception.Message.ShouldContain("Early document 'orders'");
        exception.Message.ShouldContain("Late document 'workflows'");
    }

    [Fact]
    public async Task GenerateAsync_PrefixedSourceDocument_RoutesToSourceWithItsOwnName()
    {
        // Arrange
        await using var app = await CreateAppAsync(withOpenApi: true, new FakeSource("Fake", "fake-", 0, new BuildTimeDocument("v1")));

        // Act
        var written = await GenerateAsync(app, "FAKE-V1");

        // Assert
        written.ShouldBe("Fake:v1");
    }

    [Fact]
    public async Task GenerateAsync_OpenApiDocument_WritesOpenApi()
    {
        // Arrange
        await using var app = await CreateAppAsync(withOpenApi: true, new FakeSource("Fake", "fake-", 0, new BuildTimeDocument("v1")));

        // Act
        var written = await GenerateAsync(app, "v1");

        // Assert
        JsonNode.Parse(written)!["openapi"].ShouldNotBeNull();
    }

    [Fact]
    public async Task GenerateAsync_UnknownName_ThrowsListingKnownNames()
    {
        // Arrange
        await using var app = await CreateAppAsync(withOpenApi: false, new FakeSource("Fake", "fake-", 0, new BuildTimeDocument("v1")));

        // Act
        var exception = await Should.ThrowAsync<InvalidOperationException>(() => GenerateAsync(app, "v2"));

        // Assert
        exception.Message.ShouldContain("'v2'");
        exception.Message.ShouldContain("'v1'");
    }

    [Fact]
    public async Task OpenApiDocumentProvider_RegisteredFirst_IsForwardedWithSpecVersion()
    {
        // Arrange - resolve OpenAPI's IDocumentProvider the way `dotnet getdocument` does when it finds that
        // type first: by name, through reflection, preferring the GenerateAsync overload taking a spec version.
        await using var app = await CreateAppAsync(withOpenApi: true, new FakeSource("Fake", "fake-", 0, new BuildTimeDocument("v1")));
        var serviceType = Type.GetType(OpenApiDocumentProviderInterface, throwOnError: true)!;
        var service = app.Services.GetRequiredService(serviceType);
        var getDocumentNames = serviceType.GetMethod("GetDocumentNames", Type.EmptyTypes)!;
        var generate = serviceType.GetMethod("GenerateAsync", [typeof(string), typeof(TextWriter)])!;
        var generateWithVersion = serviceType.GetMethod(
            "GenerateAsync", [typeof(string), typeof(TextWriter), typeof(OpenApiSpecVersion)])!;
        using var openApiWriter = new StringWriter();
        using var sourceWriter = new StringWriter();

        // Act
        var names = ((IEnumerable<string>)getDocumentNames.Invoke(service, null)!).ToList();
        await (Task)generateWithVersion.Invoke(service, ["v1", openApiWriter, OpenApiSpecVersion.OpenApi3_0])!;
        await (Task)generate.Invoke(service, ["fake-v1", sourceWriter])!;

        // Assert
        names.ShouldBe(["v1", "fake-v1"]);
        JsonNode.Parse(openApiWriter.ToString())!["openapi"]!.GetValue<string>().ShouldStartWith("3.0");
        sourceWriter.ToString().ShouldBe("Fake:v1");
    }

    [Fact]
    public void OpenApiDocumentProviderProxy_Create_ThrowsForNullArguments()
    {
        // Arrange
        var serviceType = Type.GetType(OpenApiDocumentProviderInterface, throwOnError: true)!;
        var target = new BuildTimeDocumentProvider(new ServiceCollection().BuildServiceProvider());

        // Act & Assert
        Should.Throw<ArgumentNullException>(() => OpenApiDocumentProviderProxy.Create(null!, target));
        Should.Throw<ArgumentNullException>(() => OpenApiDocumentProviderProxy.Create(serviceType, null!));
    }

    [Fact]
    public void AddBuildTimeDocumentSource_ThrowsForNullServices()
    {
        // Act & Assert
        Should.Throw<ArgumentNullException>(() => BuildTimeDocumentServiceCollectionExtensions.AddBuildTimeDocumentSource<TypedSource>(null!));
    }

    [Fact]
    public void AddBuildTimeDocumentSource_CalledTwice_RegistersSourceAndProviderOnce()
    {
        // Arrange
        var services = new ServiceCollection();

        // Act
        services.AddBuildTimeDocumentSource<TypedSource>().AddBuildTimeDocumentSource<TypedSource>();
        using var serviceProvider = services.BuildServiceProvider();

        // Assert
        serviceProvider.GetServices<IBuildTimeDocumentSource>().ShouldHaveSingleItem().ShouldBeOfType<TypedSource>();
        serviceProvider.GetRequiredService<IDocumentProvider>().ShouldBeOfType<BuildTimeDocumentProvider>();
        services.Count(descriptor => descriptor.ServiceType == typeof(IDocumentProvider)).ShouldBe(1);
    }
}
