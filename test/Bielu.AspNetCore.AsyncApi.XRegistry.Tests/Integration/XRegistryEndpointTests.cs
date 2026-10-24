using System.Net;
using System.Text.Json;
using Bielu.AspNetCore.AsyncApi.Attributes;
using Bielu.AspNetCore.AsyncApi.Extensions;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Shouldly;
using Xunit;

namespace Bielu.AspNetCore.AsyncApi.XRegistry.Tests.Integration;

public class XRegistryEndpointTests
{
    private const string DocumentName = "xregistry";
    private const string BaseUrl = "http://localhost/registry";
    private const string Group = $"/messagegroups/{DocumentName}";
    private const string SchemaGroup = $"/schemagroups/{DocumentName}";

    /// <summary>An order was placed.</summary>
    /// <param name="OrderId">The order.</param>
    public sealed record OrderPlaced(string OrderId, int Quantity);

    public sealed record OrderAudited(string OrderId);

    [AsyncApi(DocumentName)]
    public sealed class Channels
    {
        [Channel("orders/placed")]
        [Message(typeof(OrderPlaced), MessageId = "orderPlaced", Title = "Order placed", ContentType = "application/json")]
        [SendOperation(typeof(OrderPlaced), OperationId = "orderPlacedOperation")]
        public void OrderPlacedEvent()
        {
        }

        [Channel("orders/audited")]
        [Message(typeof(OrderAudited), MessageId = "orderAudited")]
        [SendOperation(typeof(OrderAudited), OperationId = "orderAuditedOperation")]
        public void OrderAuditedEvent()
        {
        }
    }

    [Fact]
    public async Task GetRegistry_ReturnsTheRegistryEntityWithCollectionUrls()
    {
        // Act
        using var registry = await GetJsonAsync("/registry");

        // Assert
        var root = registry.RootElement;
        root.GetProperty("specversion").GetString().ShouldBe("1.0-rc4");
        root.GetProperty("registryid").GetString().ShouldBe("orders");
        root.GetProperty("name").GetString().ShouldBe("Orders");
        root.GetProperty("self").GetString().ShouldBe(BaseUrl + "/");
        root.GetProperty("xid").GetString().ShouldBe("/");
        root.GetProperty("epoch").GetInt32().ShouldBe(1);
        root.GetProperty("createdat").GetString().ShouldEndWith("Z");
        root.GetProperty("messagegroupsurl").GetString().ShouldBe(BaseUrl + "/messagegroups");
        root.GetProperty("messagegroupscount").GetInt32().ShouldBe(1);
        root.GetProperty("schemagroupsurl").GetString().ShouldBe(BaseUrl + "/schemagroups");
        root.GetProperty("schemagroupscount").GetInt32().ShouldBe(1);
        root.TryGetProperty("messagegroups", out _).ShouldBeFalse();
    }

    [Fact]
    public async Task GetRegistry_InlineAll_InlinesEverythingButModelAndCapabilities()
    {
        // Act
        using var registry = await GetJsonAsync("/registry?inline");

        // Assert
        var root = registry.RootElement;
        root.TryGetProperty("model", out _).ShouldBeFalse();
        root.TryGetProperty("capabilities", out _).ShouldBeFalse();
        var message = root.GetProperty("messagegroups").GetProperty(DocumentName)
            .GetProperty("messages").GetProperty("orderPlaced");
        message.GetProperty("meta").GetProperty("readonly").GetBoolean().ShouldBeTrue();
        message.GetProperty("versions").GetProperty("1").GetProperty("isdefault").GetBoolean().ShouldBeTrue();
        var schema = root.GetProperty("schemagroups").GetProperty(DocumentName)
            .GetProperty("schemas").GetProperty("orderPlaced");
        schema.GetProperty("schema").GetProperty("type").GetString().ShouldBe("object");
    }

    [Fact]
    public async Task GetRegistry_InlineModelAndCapabilities_InlinesOnlyThem()
    {
        // Act
        using var registry = await GetJsonAsync("/registry?inline=model,capabilities");

        // Assert
        var root = registry.RootElement;
        root.GetProperty("model").GetProperty("groups").TryGetProperty("messagegroups", out _).ShouldBeTrue();
        root.GetProperty("capabilities").GetProperty("flags")[0].GetString().ShouldBe("inline");
        root.TryGetProperty("messagegroups", out _).ShouldBeFalse();
    }

    [Fact]
    public async Task GetRegistry_InlineNestedPath_InlinesItsParentsOnly()
    {
        // Act
        using var registry = await GetJsonAsync("/registry?inline=messagegroups.messages");

        // Assert
        var root = registry.RootElement;
        var message = root.GetProperty("messagegroups").GetProperty(DocumentName).GetProperty("messages").GetProperty("orderPlaced");
        message.TryGetProperty("versions", out _).ShouldBeFalse();
        root.TryGetProperty("schemagroups", out _).ShouldBeFalse();
    }

    [Fact]
    public async Task GetRegistry_UnknownInlinePath_IsBadInline()
    {
        // Act
        var (status, problem) = await GetProblemAsync("/registry?inline=messages");

        // Assert
        status.ShouldBe(HttpStatusCode.BadRequest);
        problem.RootElement.GetProperty("type").GetString().ShouldEndWith("#bad_inline");
    }

    [Fact]
    public async Task GetRegistry_UnsupportedFlag_IsBadFlag()
    {
        // Act
        var (status, problem) = await GetProblemAsync("/registry?filter=name=x");

        // Assert
        status.ShouldBe(HttpStatusCode.BadRequest);
        problem.RootElement.GetProperty("type").GetString().ShouldEndWith("#bad_flag");
        problem.RootElement.GetProperty("args").GetProperty("flag").GetString().ShouldBe("filter");
    }

    [Fact]
    public async Task GetMessage_ReturnsTheDefinitionPointingAtItsSchema()
    {
        // Act
        using var client = await CreateClientAsync();
        var response = await client.GetAsync($"/registry{Group}/messages/orderPlaced");

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        response.Content.Headers.ContentLocation!.ToString().ShouldBe($"{BaseUrl}{Group}/messages/orderPlaced/versions/1");
        using var message = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var root = message.RootElement;
        root.GetProperty("messageid").GetString().ShouldBe("orderPlaced");
        root.GetProperty("versionid").GetString().ShouldBe("1");
        root.GetProperty("self").GetString().ShouldBe($"{BaseUrl}{Group}/messages/orderPlaced");
        root.GetProperty("xid").GetString().ShouldBe($"{Group}/messages/orderPlaced");
        root.GetProperty("name").GetString().ShouldBe("Order placed");
        root.GetProperty("datacontenttype").GetString().ShouldBe("application/json");
        root.GetProperty("dataschemaformat").GetString().ShouldBe("JsonSchema/draft-07");
        root.GetProperty("dataschemaxid").GetString().ShouldBe($"{SchemaGroup}/schemas/orderPlaced");
        root.GetProperty("dataschemauri").GetString().ShouldBe($"{BaseUrl}{SchemaGroup}/schemas/orderPlaced");
        root.GetProperty("metaurl").GetString().ShouldBe($"{BaseUrl}{Group}/messages/orderPlaced/meta");
        root.GetProperty("versionscount").GetInt32().ShouldBe(1);
    }

    [Fact]
    public async Task GetSchema_ReturnsTheDocumentWithXRegistryHeaders()
    {
        // Act
        using var client = await CreateClientAsync();
        var response = await client.GetAsync($"/registry{SchemaGroup}/schemas/orderPlaced");

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        response.Content.Headers.ContentType!.MediaType.ShouldBe("application/schema+json");
        response.Headers.GetValues("xRegistry-schemaid").Single().ShouldBe("orderPlaced");
        response.Headers.GetValues("xRegistry-versionid").Single().ShouldBe("1");
        response.Headers.GetValues("xRegistry-self").Single().ShouldBe($"{BaseUrl}{SchemaGroup}/schemas/orderPlaced$details");
        response.Headers.GetValues("xRegistry-format").Single().ShouldBe("JsonSchema/draft-07");
        response.Headers.GetValues("xRegistry-versionscount").Single().ShouldBe("1");
        response.Content.Headers.ContentDisposition!.FileName!.Trim('"').ShouldBe("orderPlaced");

        using var schema = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        schema.RootElement.GetProperty("$schema").GetString().ShouldBe("http://json-schema.org/draft-07/schema#");
        schema.RootElement.GetProperty("properties").TryGetProperty("orderId", out _).ShouldBeTrue();
    }

    [Fact]
    public async Task GetSchemaDetails_ReturnsItsMetadata()
    {
        // Act
        using var schema = await GetJsonAsync($"/registry{SchemaGroup}/schemas/orderPlaced$details?inline=schema");

        // Assert
        var root = schema.RootElement;
        root.GetProperty("schemaid").GetString().ShouldBe("orderPlaced");
        root.GetProperty("self").GetString().ShouldBe($"{BaseUrl}{SchemaGroup}/schemas/orderPlaced$details");
        root.GetProperty("format").GetString().ShouldBe("JsonSchema/draft-07");
        root.GetProperty("contenttype").GetString().ShouldBe("application/schema+json");
        root.GetProperty("schema").GetProperty("type").GetString().ShouldBe("object");
    }

    [Theory]
    [InlineData($"/registry{Group}/messages/orderPlaced/meta", "defaultversionid")]
    [InlineData($"/registry{Group}/messages/orderPlaced/versions", "1")]
    [InlineData($"/registry{Group}/messages/orderPlaced/versions/1", "versionid")]
    [InlineData($"/registry{SchemaGroup}/schemas/orderPlaced/versions/1$details", "versionid")]
    [InlineData($"/registry{SchemaGroup}/schemas", "orderAudited")]
    [InlineData($"/registry{Group}/messages", "orderAudited")]
    [InlineData("/registry/messagegroups", DocumentName)]
    [InlineData($"/registry{Group}", "messagesurl")]
    [InlineData("/registry/capabilities", "specversions")]
    [InlineData("/registry/model", "groups")]
    public async Task GetEntity_ReturnsIt(string path, string expectedProperty)
    {
        // Act
        using var entity = await GetJsonAsync(path);

        // Assert
        entity.RootElement.TryGetProperty(expectedProperty, out _).ShouldBeTrue();
    }

    [Fact]
    public async Task GetSchemaVersion_ReturnsTheDocument()
    {
        // Act
        using var client = await CreateClientAsync();
        var response = await client.GetAsync($"/registry{SchemaGroup}/schemas/orderPlaced/versions/1");

        // Assert
        response.Content.Headers.ContentType!.MediaType.ShouldBe("application/schema+json");
        response.Headers.GetValues("xRegistry-xid").Single().ShouldBe($"{SchemaGroup}/schemas/orderPlaced/versions/1");
    }

    [Theory]
    [InlineData("/registry/messagegroups/missing")]
    [InlineData($"/registry{Group}/messages/missing")]
    [InlineData($"/registry{Group}/messages/orderPlaced/versions/2")]
    [InlineData($"/registry{Group}/schemas")]
    [InlineData("/registry/endpoints")]
    public async Task GetMissingEntity_IsNotFound(string path)
    {
        // Act
        var (status, problem) = await GetProblemAsync(path);

        // Assert
        status.ShouldBe(HttpStatusCode.NotFound);
        problem.RootElement.GetProperty("type").GetString()
            .ShouldBe("https://github.com/xregistry/spec/blob/main/core/spec.md#not_found");
    }

    [Fact]
    public async Task GetWellKnown_ListsTheRegistry()
    {
        // Act
        using var discovery = await GetJsonAsync("/.well-known/xregistry");

        // Assert
        discovery.RootElement.GetProperty("registries")[0].GetString().ShouldBe(BaseUrl);
    }

    [Fact]
    public async Task IncludeDocuments_LeavesOtherDocumentsOut()
    {
        // Act
        using var client = await CreateClientAsync(options => options.IncludeDocuments("other"));
        using var registry = JsonDocument.Parse(await client.GetStringAsync("/registry"));

        // Assert
        registry.RootElement.GetProperty("messagegroupscount").GetInt32().ShouldBe(0);
    }

    [Fact]
    public void MapXRegistry_ValidatesArguments()
    {
        // Act & Assert
        Should.Throw<ArgumentNullException>(() => XRegistryEndpointRouteBuilderExtensions.MapXRegistry(null!));
    }

    private static async Task<JsonDocument> GetJsonAsync(string path)
    {
        using var client = await CreateClientAsync();
        var response = await client.GetAsync(path);
        var body = await response.Content.ReadAsStringAsync();
        response.StatusCode.ShouldBe(HttpStatusCode.OK, body);
        response.Content.Headers.ContentType!.MediaType.ShouldBe("application/json");
        return JsonDocument.Parse(body);
    }

    private static async Task<(HttpStatusCode Status, JsonDocument Problem)> GetProblemAsync(string path)
    {
        using var client = await CreateClientAsync();
        var response = await client.GetAsync(path);
        return (response.StatusCode, JsonDocument.Parse(await response.Content.ReadAsStringAsync()));
    }

    private static async Task<HttpClient> CreateClientAsync(Action<XRegistryOptions>? configure = null)
    {
        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseTestServer();
        builder.Services.AddAsyncApi(DocumentName, options => options.WithInfo("Orders", "1.0.0"));
        builder.Services.AddXRegistry(options =>
        {
            options.WithRegistry("orders", "Orders");
            configure?.Invoke(options);
        });

        var app = builder.Build();
        app.MapXRegistry();
        await app.StartAsync();
        return app.GetTestClient();
    }
}
