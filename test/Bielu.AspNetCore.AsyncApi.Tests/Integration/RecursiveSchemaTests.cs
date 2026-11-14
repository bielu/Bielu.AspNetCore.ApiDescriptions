using System.ComponentModel;
using System.Net;
using System.Text.Json;
using Bielu.AspNetCore.AsyncApi.Attributes;
using Bielu.AspNetCore.AsyncApi.Extensions;
using Bielu.AspNetCore.AsyncApi.Services;
using ByteBard.AsyncAPI;
using ByteBard.AsyncAPI.Readers;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Shouldly;
using Xunit;

namespace Bielu.AspNetCore.AsyncApi.Tests.Integration;

/// <summary>
/// Recursive payload types. The exporter describes a recursion as a <c>$ref</c> back to the type's first
/// occurrence; that used to be inlined one level and then emitted as an empty schema (<c>"items": {}</c>), so a
/// tree was documented as two levels deep with untyped grandchildren.
/// </summary>
public class RecursiveSchemaTests
{
    private const string DocumentName = "recursive-schemas";
    private const string V2DocumentName = "recursive-schemas-v2";

    public sealed class RecursiveTreeNode
    {
        public string Label { get; set; } = string.Empty;

        public List<RecursiveTreeNode> Children { get; set; } = [];
    }

    public sealed class RecursiveCatalogEntry
    {
        public string Sku { get; set; } = string.Empty;

        [Description("The category the entry is listed under.")]
        public RecursiveCategory Category { get; set; } = new();
    }

    public sealed class RecursiveCategory
    {
        public string Name { get; set; } = string.Empty;

        public List<RecursiveCategory> Subcategories { get; set; } = [];

        public RecursiveCategory? Parent { get; set; }
    }

    public sealed class RecursivePerson
    {
        public string Name { get; set; } = string.Empty;

        public RecursivePet? Pet { get; set; }
    }

    public sealed class RecursivePet
    {
        public string Name { get; set; } = string.Empty;

        public RecursivePerson? Owner { get; set; }
    }

    [AsyncApi(DocumentName)]
    public sealed class Channels
    {
        [Channel("recursive/tree")]
        [Message(typeof(RecursiveTreeNode), MessageId = "recursiveTree")]
        [SendOperation(OperationId = "recursiveTreeOperation")]
        public void Tree()
        {
        }

        [Channel("recursive/catalog")]
        [Message(typeof(RecursiveCatalogEntry), MessageId = "recursiveCatalog")]
        [SendOperation(OperationId = "recursiveCatalogOperation")]
        public void Catalog()
        {
        }

        [Channel("recursive/people")]
        [Message(typeof(RecursivePerson), MessageId = "recursivePerson")]
        [SendOperation(OperationId = "recursivePersonOperation")]
        public void People()
        {
        }
    }

    /// <summary>The same payloads in an AsyncAPI 2.6 document, which references schemas the same way.</summary>
    [AsyncApi(V2DocumentName)]
    public sealed class V2Channels
    {
        [Channel("recursive/tree")]
        [Message(typeof(RecursiveTreeNode), MessageId = "recursiveTree")]
        [SendOperation(OperationId = "recursiveTreeOperation")]
        public void Tree()
        {
        }

        [Channel("recursive/catalog")]
        [Message(typeof(RecursiveCatalogEntry), MessageId = "recursiveCatalog")]
        [SendOperation(OperationId = "recursiveCatalogOperation")]
        public void Catalog()
        {
        }
    }

    [Fact]
    public async Task GetAsyncApiDocument_SelfReferencingPayload_ReferencesItsOwnComponent()
    {
        // Act
        var schemas = await GetSchemasAsync(DocumentName);

        // Assert
        var children = schemas.GetProperty("recursiveTreeNode").GetProperty("properties").GetProperty("children");
        children.GetProperty("items").GetProperty("$ref").GetString()
            .ShouldBe("#/components/schemas/recursiveTreeNode");
    }

    [Fact]
    public async Task GetAsyncApiDocument_RecursiveTypeBelowTheRoot_StaysInlineAndBecomesItsOwnComponent()
    {
        // Act
        var schemas = await GetSchemasAsync(DocumentName);

        // Assert
        var category = schemas.GetProperty("recursiveCatalogEntry").GetProperty("properties").GetProperty("category");
        category.GetProperty("description").GetString().ShouldBe("The category the entry is listed under.");
        AssertCategoryRecursesIntoComponent(category);

        var component = schemas.GetProperty("recursiveCategory");
        AssertCategoryRecursesIntoComponent(component);
    }

    [Fact]
    public async Task GetAsyncApiDocument_MutuallyRecursivePayloads_ReferenceTheRecursedType()
    {
        // Act
        var schemas = await GetSchemasAsync(DocumentName);

        // Assert
        var pet = schemas.GetProperty("recursivePerson").GetProperty("properties").GetProperty("pet");
        pet.GetProperty("properties").GetProperty("name").GetProperty("type").GetString().ShouldBe("string");
        pet.GetProperty("properties").GetProperty("owner").GetProperty("$ref").GetString()
            .ShouldBe("#/components/schemas/recursivePerson");
    }

    [Theory]
    [InlineData(DocumentName)]
    [InlineData(V2DocumentName)]
    public async Task GetAsyncApiDocument_RecursivePayloads_EveryReferenceResolves(string documentName)
    {
        // Act
        var content = await GetDocumentJsonAsync(documentName);

        // Assert
        var document = new AsyncApiStringReader().Read(content, out var diagnostic);
        document.ShouldNotBeNull();
        diagnostic.Errors.ShouldBeEmpty(string.Join(Environment.NewLine, diagnostic.Errors.Select(error => error.Message)));
    }

    [Fact]
    public async Task GetAsyncApiDocument_RecursivePayload_RunsSchemaTransformersOnceOnTheBackReferenceTarget()
    {
        // Arrange
        var visitedTypes = new List<Type>();

        // Act
        await GetSchemasAsync(DocumentName, options => options.AddSchemaTransformer((_, context, _) =>
        {
            lock (visitedTypes)
            {
                visitedTypes.Add(context.JsonTypeInfo.Type);
            }

            return Task.CompletedTask;
        }));

        // Assert: the root and its one inline level, never the reference placeholders.
        visitedTypes.Count(type => type == typeof(RecursiveTreeNode)).ShouldBe(1);
    }

    private static void AssertCategoryRecursesIntoComponent(JsonElement category)
    {
        var properties = category.GetProperty("properties");
        properties.GetProperty("name").GetProperty("type").GetString().ShouldBe("string");
        properties.GetProperty("subcategories").GetProperty("items").GetProperty("$ref").GetString()
            .ShouldBe("#/components/schemas/recursiveCategory");
        properties.GetProperty("parent").GetProperty("$ref").GetString()
            .ShouldBe("#/components/schemas/recursiveCategory");
    }

    private static async Task<JsonElement> GetSchemasAsync(string documentName, Action<AsyncApiOptions>? configure = null)
    {
        var content = await GetDocumentJsonAsync(documentName, configure);
        return JsonDocument.Parse(content).RootElement.GetProperty("components").GetProperty("schemas").Clone();
    }

    private static async Task<string> GetDocumentJsonAsync(string documentName, Action<AsyncApiOptions>? configure = null)
    {
        // Arrange
        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseTestServer();
        builder.Services.AddAsyncApi(documentName, options =>
        {
            if (documentName == V2DocumentName)
            {
                options.AsyncApiVersion = AsyncApiVersion.AsyncApi2_0;
            }

            configure?.Invoke(options);
        });

        await using var app = builder.Build();
        app.MapAsyncApi();
        await app.StartAsync();

        var response = await app.GetTestClient()
            .GetAsync(AsyncApiGeneratorConstants.DefaultAsyncApiRoute.Replace("{documentName}", documentName));
        var content = await response.Content.ReadAsStringAsync();
        response.StatusCode.ShouldBe(HttpStatusCode.OK, content);
        await app.StopAsync();

        return content;
    }
}
