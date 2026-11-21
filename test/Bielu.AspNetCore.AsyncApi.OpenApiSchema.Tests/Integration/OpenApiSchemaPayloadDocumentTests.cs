using System.Text.Json;
using Bielu.AspNetCore.AsyncApi.OpenApiSchema.Tests.Fixtures;
using Bielu.AspNetCore.AsyncApi.Services;
using ByteBard.AsyncAPI;
using ByteBard.AsyncAPI.Models;
using Json.Schema;
using Shouldly;
using Xunit;

namespace Bielu.AspNetCore.AsyncApi.OpenApiSchema.Tests.Integration;

public class OpenApiSchemaPayloadDocumentTests
{
    [Fact]
    public async Task AddOpenApiSchemaPayloads_ReplacesThePayloadWithAnOpenApiSchema()
    {
        // Act
        using var document = await OpenApiTestApp.GetDocumentAsync(options => options.AddOpenApiSchemaPayloads());

        // Assert
        var payload = Message(document, "orderPlaced").GetProperty("payload");
        payload.GetProperty("schemaFormat").GetString().ShouldBe("application/vnd.oai.openapi;version=3.0.0");
        var schema = payload.GetProperty("schema");
        schema.GetProperty("type").GetString().ShouldBe("object");

        var properties = schema.GetProperty("properties");
        properties.GetProperty("orderId").GetProperty("format").GetString().ShouldBe("uuid");
        var note = properties.GetProperty("note");
        note.GetProperty("type").GetString().ShouldBe("string");
        note.GetProperty("nullable").GetBoolean().ShouldBeTrue();
        var total = properties.GetProperty("total");
        total.GetProperty("minimum").GetDouble().ShouldBe(0);
        total.GetProperty("exclusiveMinimum").GetBoolean().ShouldBeTrue();
        total.GetProperty("maximum").GetDouble().ShouldBe(1000);
        properties.GetProperty("lines").GetProperty("items").GetProperty("type").GetString().ShouldBe("object");
        properties.GetProperty("attributes").GetProperty("additionalProperties").GetProperty("type").GetString().ShouldBe("string");
    }

    [Fact]
    public async Task AddOpenApiSchemaPayloads_InlinesReferencesAndPointsARecursiveOneAtAnOpenApiComponent()
    {
        // Act
        using var document = await OpenApiTestApp.GetDocumentAsync(options => options.AddOpenApiSchemaPayloads());

        // Assert
        var category = Message(document, "orderPlaced").GetProperty("payload").GetProperty("schema")
            .GetProperty("properties").GetProperty("category");
        var child = category.GetProperty("properties").GetProperty("children").GetProperty("items");
        child.GetProperty("type").GetString().ShouldBe("object");
        child.GetProperty("properties").GetProperty("children").GetProperty("items").GetProperty("$ref").GetString()
            .ShouldBe("#/components/schemas/category.openapi/schema");

        var schemas = document.RootElement.GetProperty("components").GetProperty("schemas");
        schemas.TryGetProperty("category", out _).ShouldBeTrue();
        var copy = schemas.GetProperty("category.openapi");
        copy.GetProperty("schemaFormat").GetString().ShouldBe("application/vnd.oai.openapi;version=3.0.0");
        copy.GetProperty("schema").GetProperty("properties").GetProperty("children").GetProperty("items")
            .GetProperty("$ref").GetString().ShouldBe("#/components/schemas/category.openapi/schema");
    }

    [Fact]
    public async Task AddOpenApiSchemaPayloads_AsyncApi2_PointsARecursiveReferenceAtTheJsonSchemaComponent()
    {
        // Act
        using var document = await OpenApiTestApp.GetDocumentAsync(options =>
        {
            options.AsyncApiVersion = AsyncApiVersion.AsyncApi2_0;
            options.AddOpenApiSchemaPayloads();
        });

        // Assert
        var child = Message(document, "orderPlaced").GetProperty("payload").GetProperty("properties")
            .GetProperty("category").GetProperty("properties").GetProperty("children").GetProperty("items");
        child.GetProperty("properties").GetProperty("children").GetProperty("items").GetProperty("$ref").GetString()
            .ShouldBe("#/components/schemas/category");
        var copy = document.RootElement.GetProperty("components").GetProperty("schemas").GetProperty("category.openapi");
        copy.TryGetProperty("schemaFormat", out _).ShouldBeFalse();
        copy.GetProperty("properties").GetProperty("children").GetProperty("items").GetProperty("$ref").GetString()
            .ShouldBe("#/components/schemas/category");
    }

    [Fact]
    public async Task AddOpenApiSchemaPayloads_KeepsThePayloadWrapperExtensions()
    {
        // Act
        using var document = await OpenApiTestApp.GetDocumentAsync(options =>
        {
            options.AddDocumentTransformer((asyncApi, _, _) =>
            {
                asyncApi.Components.Messages["orderPlaced"].Payload.Extensions["x-owner"] = new AsyncApiAny("orders");
                return Task.CompletedTask;
            });
            options.AddOpenApiSchemaPayloads();
        });

        // Assert
        var payload = Message(document, "orderPlaced").GetProperty("payload");
        payload.GetProperty("schemaFormat").GetString().ShouldBe("application/vnd.oai.openapi;version=3.0.0");
        payload.GetProperty("x-owner").GetString().ShouldBe("orders");
    }

    [Fact]
    public async Task AddOpenApiSchemaPayloads_WithMessageFilter_LeavesOtherMessagesAsJsonSchema()
    {
        // Act
        using var document = await OpenApiTestApp.GetDocumentAsync(options =>
            options.AddOpenApiSchemaPayloads(openApi => openApi.ForMessages("orderPlaced")));

        // Assert
        Message(document, "orderPlaced").GetProperty("payload").TryGetProperty("schemaFormat", out _).ShouldBeTrue();
        Message(document, "orderAudited").GetProperty("payload").GetProperty("$ref").GetString()
            .ShouldBe("#/components/schemas/orderAudited");
    }

    [Fact]
    public async Task AddOpenApiSchemaPayloads_AsyncApi2_PutsTheSchemaFormatOnTheMessage()
    {
        // Act
        using var document = await OpenApiTestApp.GetDocumentAsync(options =>
        {
            options.AsyncApiVersion = AsyncApiVersion.AsyncApi2_0;
            options.AddOpenApiSchemaPayloads();
        });

        // Assert
        var message = Message(document, "orderAudited");
        message.GetProperty("schemaFormat").GetString().ShouldBe("application/vnd.oai.openapi;version=3.0.0");
        message.GetProperty("payload").GetProperty("type").GetString().ShouldBe("object");
    }

    [Theory]
    [InlineData(AsyncApiVersion.AsyncApi2_0)]
    [InlineData(AsyncApiVersion.AsyncApi3_0)]
    public async Task ServedDocument_ConformsToOfficialSchema(AsyncApiVersion version)
    {
        // Act
        using var document = await OpenApiTestApp.GetDocumentAsync(options =>
        {
            options.AsyncApiVersion = version;
            options.AddOpenApiSchemaPayloads();
        });

        // Assert
        var declaredVersion = document.RootElement.GetProperty("asyncapi").GetString()!;
        var schemaPath = Path.Combine(AppContext.BaseDirectory, "Conformance", "asyncapi", $"{declaredVersion}.json");
        var result = JsonSchema.FromText(File.ReadAllText(schemaPath))
            .Evaluate(document.RootElement, new EvaluationOptions { OutputFormat = OutputFormat.List });
        result.IsValid.ShouldBeTrue(string.Join(Environment.NewLine, (result.Details ?? [])
            .Where(detail => detail.Errors is { Count: > 0 })
            .SelectMany(detail => detail.Errors!.Select(error => $"{detail.InstanceLocation} {error.Value}"))));
    }

    [Fact]
    public void AddOpenApiSchemaPayloads_ThrowsForNullOptions()
    {
        // Act & Assert
        Should.Throw<ArgumentNullException>(() => AsyncApiOpenApiSchemaOptionsExtensions.AddOpenApiSchemaPayloads(null!));
    }

    [Fact]
    public void AddOpenApiSchemaPayloads_RegistersADocumentTransformer()
    {
        // Arrange
        var options = new AsyncApiOptions();

        // Act
        var result = options.AddOpenApiSchemaPayloads();

        // Assert
        result.ShouldBeSameAs(options);
        options.DocumentTransformers.ShouldHaveSingleItem().ShouldBeOfType<OpenApiSchemaPayloadDocumentTransformer>();
    }

    [Fact]
    public void OpenApiSchemaPayloadOptions_ValidateArguments()
    {
        // Act & Assert
        Should.Throw<ArgumentNullException>(() => new OpenApiSchemaPayloadOptions().ForMessages(null!));
        Should.Throw<ArgumentException>(() => new OpenApiSchemaPayloadOptions().ForMessages("ok", " "));
    }

    private static JsonElement Message(JsonDocument document, string key) =>
        document.RootElement.GetProperty("components").GetProperty("messages").GetProperty(key);
}
