using System.Net;
using System.Text.Json;
using Bielu.AspNetCore.Arazzo.Extensions;
using Bielu.AspNetCore.AsyncApi.Extensions;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Shouldly;
using Xunit;

namespace Bielu.AspNetCore.ApiCatalog.Tests.Integration;

public class ApiCatalogEndpointTests
{
    private const string BaseUrl = "http://localhost";

    [Fact]
    public async Task Get_WithAsyncApiOpenApiAndArazzo_ListsEveryDocument()
    {
        // Arrange
        using var host = await CreateHostAsync(
            services =>
            {
                services.AddOpenApi();
                services.AddAsyncApi();
                services.AddAsyncApi("internal");
                services.AddArazzo(options =>
                {
                    options.ValidateSourceReferencesOnStartup = false;
                    options.AddWorkflow("noop", wf => wf.Step("only", s => s.Workflow("noop")));
                });
            },
            endpoints =>
            {
                endpoints.MapOpenApi();
                endpoints.MapAsyncApi();
                endpoints.MapArazzo();
            });

        // Act
        var links = await GetServiceDescriptionsAsync(host);

        // Assert
        links.ShouldBe([
            ($"{BaseUrl}/openapi/v1.json", "application/vnd.oai.openapi+json", "OpenAPI document 'v1'"),
            ($"{BaseUrl}/asyncapi/internal.json", "application/vnd.aai.asyncapi+json", "AsyncAPI document 'internal'"),
            ($"{BaseUrl}/asyncapi/v1.json", "application/vnd.aai.asyncapi+json", "AsyncAPI document 'v1'"),
            ($"{BaseUrl}/arazzo/v1.json", "application/vnd.oai.workflows+json", "Arazzo document 'v1'"),
        ]);
    }

    [Fact]
    public async Task Get_ServesLinksetWithProfileAndApiCatalogLinkHeader()
    {
        // Arrange
        using var host = await CreateHostAsync(services => services.AddAsyncApi(), endpoints => endpoints.MapAsyncApi());

        // Act
        var response = await host.GetTestClient().GetAsync(ApiCatalogDefaults.DefaultRoute);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        response.Content.Headers.ContentType!.MediaType.ShouldBe("application/linkset+json");
        response.Content.Headers.ContentType.Parameters
            .ShouldContain(p => p.Name == "profile" && p.Value == $"\"{ApiCatalogDefaults.Profile}\"");
        response.Headers.GetValues("Link")
            .ShouldBe([$"<{BaseUrl}{ApiCatalogDefaults.DefaultRoute}>; rel=\"api-catalog\""]);

        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var catalog = document.RootElement.GetProperty("linkset")[0];
        catalog.GetProperty("anchor").GetString().ShouldBe($"{BaseUrl}{ApiCatalogDefaults.DefaultRoute}");
        catalog.GetProperty("item")[0].GetProperty("href").GetString().ShouldBe($"{BaseUrl}/");
        document.RootElement.GetProperty("linkset")[1].GetProperty("anchor").GetString().ShouldBe($"{BaseUrl}/");
    }

    [Fact]
    public async Task Head_ReturnsLinkHeaderWithoutBody()
    {
        // Arrange
        using var host = await CreateHostAsync(services => services.AddAsyncApi(), endpoints => endpoints.MapAsyncApi());

        // Act
        var response = await host.GetTestClient().SendAsync(new HttpRequestMessage(HttpMethod.Head, ApiCatalogDefaults.DefaultRoute));

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        response.Headers.GetValues("Link").Single().ShouldContain("rel=\"api-catalog\"");
        (await response.Content.ReadAsByteArrayAsync()).ShouldBeEmpty();
    }

    [Fact]
    public async Task Get_WithYamlAsyncApiRoute_UsesYamlMediaType()
    {
        // Arrange
        using var host = await CreateHostAsync(services => services.AddAsyncApi(),
            endpoints => endpoints.MapAsyncApi("/docs/{documentName}.yaml"));

        // Act
        var links = await GetServiceDescriptionsAsync(host);

        // Assert
        links.ShouldBe([($"{BaseUrl}/docs/v1.yaml", "application/vnd.aai.asyncapi+yaml", "AsyncAPI document 'v1'")]);
    }

    [Fact]
    public async Task Get_WithRouteWithoutDocumentName_ListsTheRouteOnce()
    {
        // Arrange
        using var host = await CreateHostAsync(services => services.AddAsyncApi(),
            endpoints => endpoints.MapAsyncApi("/asyncapi.json"));

        // Act
        var links = await GetServiceDescriptionsAsync(host);

        // Assert
        links.ShouldBe([($"{BaseUrl}/asyncapi.json", "application/vnd.aai.asyncapi+json", "AsyncAPI document")]);
    }

    [Fact]
    public async Task Get_WithOpenApiRoutePatternNull_LeavesOpenApiOut()
    {
        // Arrange
        using var host = await CreateHostAsync(
            services =>
            {
                services.AddOpenApi();
                services.AddApiCatalog(options => options.OpenApiRoutePattern = null);
            },
            endpoints => endpoints.MapOpenApi());

        // Act
        var links = await GetServiceDescriptionsAsync(host);

        // Assert
        links.ShouldBeEmpty();
    }

    [Fact]
    public async Task Get_WithCustomOpenApiRoute_ListsOpenApiWhenTheOptionMatches()
    {
        // Arrange
        using var host = await CreateHostAsync(
            services =>
            {
                services.AddOpenApi();
                services.AddApiCatalog(options => options.OpenApiRoutePattern = "/specs/{documentName}.yaml");
            },
            endpoints => endpoints.MapOpenApi("/specs/{documentName}.yaml"));

        // Act
        var links = await GetServiceDescriptionsAsync(host);

        // Assert
        links.ShouldBe([($"{BaseUrl}/specs/v1.yaml", "application/vnd.oai.openapi+yaml", "OpenAPI document 'v1'")]);
    }

    [Fact]
    public async Task Get_WithoutDocuments_ListsOnlyConfiguredItems()
    {
        // Arrange
        using var host = await CreateHostAsync(
            services => services.AddApiCatalog(options => options
                .AddItem("https://payments.example.com/.well-known/api-catalog", "Payments")),
            _ => { });

        // Act
        var json = await host.GetTestClient().GetStringAsync(ApiCatalogDefaults.DefaultRoute);

        // Assert
        using var document = JsonDocument.Parse(json);
        var linkset = document.RootElement.GetProperty("linkset");
        linkset.GetArrayLength().ShouldBe(1);
        var item = linkset[0].GetProperty("item").EnumerateArray().Single();
        item.GetProperty("href").GetString().ShouldBe("https://payments.example.com/.well-known/api-catalog");
        item.GetProperty("title").GetString().ShouldBe("Payments");
    }

    [Fact]
    public async Task Get_WithServiceDoc_ResolvesItAgainstThePathBase()
    {
        // Arrange
        using var host = await CreateHostAsync(
            services =>
            {
                services.AddAsyncApi();
                services.AddApiCatalog(options => options.AddServiceDoc("/scalar", "API reference"));
            },
            endpoints => endpoints.MapAsyncApi(),
            pathBase: "/gateway");

        // Act
        var json = await host.GetTestClient().GetStringAsync($"/gateway{ApiCatalogDefaults.DefaultRoute}");

        // Assert
        using var document = JsonDocument.Parse(json);
        var api = document.RootElement.GetProperty("linkset")[1];
        api.GetProperty("anchor").GetString().ShouldBe($"{BaseUrl}/gateway/");
        api.GetProperty("service-desc")[0].GetProperty("href").GetString().ShouldBe($"{BaseUrl}/gateway/asyncapi/v1.json");
        var serviceDoc = api.GetProperty("service-doc").EnumerateArray().Single();
        serviceDoc.GetProperty("href").GetString().ShouldBe($"{BaseUrl}/gateway/scalar");
        serviceDoc.GetProperty("type").GetString().ShouldBe("text/html");
        serviceDoc.GetProperty("title").GetString().ShouldBe("API reference");
    }

    [Theory]
    [InlineData("/api", $"{BaseUrl}/gateway/api")]
    [InlineData("https://api.example.com/", "https://api.example.com/")]
    public async Task Get_WithApiUrl_PointsItemAndAnchorAtIt(string apiUrl, string expected)
    {
        // Arrange
        using var host = await CreateHostAsync(
            services =>
            {
                services.AddAsyncApi();
                services.AddApiCatalog(options => options.ApiUrl = apiUrl);
            },
            endpoints => endpoints.MapAsyncApi(),
            pathBase: "/gateway");

        // Act
        var json = await host.GetTestClient().GetStringAsync($"/gateway{ApiCatalogDefaults.DefaultRoute}");

        // Assert
        using var document = JsonDocument.Parse(json);
        var linkset = document.RootElement.GetProperty("linkset");
        linkset[0].GetProperty("item").EnumerateArray().Single().GetProperty("href").GetString().ShouldBe(expected);
        linkset[1].GetProperty("anchor").GetString().ShouldBe(expected);
        linkset[1].GetProperty("service-desc")[0].GetProperty("href").GetString()
            .ShouldBe($"{BaseUrl}/gateway/asyncapi/v1.json");
    }

    [Fact]
    public async Task Get_WithEndpointsMarkedAsItems_ListsEachRouteOnce()
    {
        // Arrange: a group like MapMcp()'s, with several methods on one route, and a route that cannot be linked to.
        using var host = await CreateHostAsync(
            services => services.AddAsyncApi(),
            endpoints =>
            {
                endpoints.MapAsyncApi();
                var mcp = endpoints.MapGroup("/mcp");
                mcp.MapPost("", () => Results.Ok());
                mcp.MapGet("", () => Results.Ok());
                mcp.MapDelete("", () => Results.Ok());
                mcp.WithApiCatalogItem("MCP server");
                endpoints.MapGet("/graphql", () => Results.Ok()).WithApiCatalogItem();
                endpoints.MapGet("/items/{id}", (string id) => Results.Ok()).WithApiCatalogItem("Unlinkable");
            });

        // Act
        var json = await host.GetTestClient().GetStringAsync(ApiCatalogDefaults.DefaultRoute);

        // Assert
        using var document = JsonDocument.Parse(json);
        var items = document.RootElement.GetProperty("linkset")[0].GetProperty("item").EnumerateArray()
            .Select(item => (item.GetProperty("href").GetString(), item.TryGetProperty("title", out var title) ? title.GetString() : null))
            .ToList();
        items.ShouldBe([($"{BaseUrl}/", null), ($"{BaseUrl}/mcp", "MCP server"), ($"{BaseUrl}/graphql", null)], ignoreOrder: true);
    }

    [Fact]
    public async Task Get_WithOnlyEndpointItems_ListsThemWithoutTheAppsApi()
    {
        // Arrange
        using var host = await CreateHostAsync(_ => { },
            endpoints => endpoints.MapPost("/mcp", () => Results.Ok()).WithApiCatalogItem("MCP server"));

        // Act
        var json = await host.GetTestClient().GetStringAsync(ApiCatalogDefaults.DefaultRoute);

        // Assert
        using var document = JsonDocument.Parse(json);
        var linkset = document.RootElement.GetProperty("linkset");
        linkset.GetArrayLength().ShouldBe(1);
        linkset[0].GetProperty("item").EnumerateArray().Single().GetProperty("href").GetString().ShouldBe($"{BaseUrl}/mcp");
    }

    [Fact]
    public void WithApiCatalogItem_ThrowsForNullBuilder()
    {
        // Act & Assert
        Should.Throw<ArgumentNullException>(() =>
            ApiCatalogEndpointConventionBuilderExtensions.WithApiCatalogItem<IEndpointConventionBuilder>(null!));
    }

    [Fact]
    public void MapApiCatalog_ThrowsForNullEndpoints()
    {
        // Act & Assert
        Should.Throw<ArgumentNullException>(() => ApiCatalogEndpointRouteBuilderExtensions.MapApiCatalog(null!));
    }

    [Fact]
    public async Task MapApiCatalog_ThrowsForEmptyPattern()
    {
        // Act & Assert
        await Should.ThrowAsync<ArgumentException>(() => CreateHostAsync(_ => { }, endpoints => { }, catalogPattern: " "));
    }

    private static async Task<List<(string Href, string Type, string Title)>> GetServiceDescriptionsAsync(IHost host)
    {
        var json = await host.GetTestClient().GetStringAsync(ApiCatalogDefaults.DefaultRoute);
        using var document = JsonDocument.Parse(json);
        var linkset = document.RootElement.GetProperty("linkset");
        if (linkset.GetArrayLength() < 2 || !linkset[1].TryGetProperty("service-desc", out var serviceDescriptions))
        {
            return [];
        }

        return serviceDescriptions.EnumerateArray()
            .Select(link => (link.GetProperty("href").GetString()!, link.GetProperty("type").GetString()!,
                link.GetProperty("title").GetString()!))
            .ToList();
    }

    private static async Task<IHost> CreateHostAsync(Action<IServiceCollection> configureServices,
        Action<IEndpointRouteBuilder> mapDocuments, string? pathBase = null,
        string catalogPattern = ApiCatalogDefaults.DefaultRoute)
    {
        var host = Host.CreateDefaultBuilder()
            .ConfigureWebHostDefaults(webBuilder =>
            {
                webBuilder.UseTestServer();
                webBuilder.ConfigureServices(services =>
                {
                    services.AddRouting();
                    configureServices(services);
                });
                webBuilder.Configure(app =>
                {
                    if (pathBase is not null)
                    {
                        app.UsePathBase(pathBase);
                    }

                    app.UseRouting();
                    app.UseEndpoints(endpoints =>
                    {
                        mapDocuments(endpoints);
                        endpoints.MapApiCatalog(catalogPattern);
                    });
                });
            })
            .Build();
        await host.StartAsync();
        return host;
    }
}
