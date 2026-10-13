using System.Text.Json.Nodes;
using Bielu.AspNetCore.AsyncApi.Wolverine.Tests.Fixtures;
using ByteBard.AsyncAPI;
using ByteBard.AsyncAPI.Models;
using Shouldly;
using Xunit;

namespace Bielu.AspNetCore.AsyncApi.Wolverine.Tests.Integration;

/// <summary>
/// Spec metadata that Wolverine's routing can't supply, declared on <see cref="WolverineAsyncApiOptions"/>:
/// the correlation id, external docs, traits and security requirements.
/// </summary>
public class WolverineSpecMetadataTests
{
    private const string SendOperation = "send.orders.order-placed";
    private const string ReceiveOperation = "receive.stock.stock-changed";

    [Fact]
    public async Task TransformAsync_IncludeCorrelationId_AddsCorrelationIdToEveryMessage()
    {
        // Act
        var document = await GetDocumentAsync(wolverine => wolverine.IncludeCorrelationId());

        // Assert
        foreach (var key in new[] { "orders.order-placed", "stock.stock-changed" })
        {
            document["components"]!["messages"]![key]!["correlationId"]!["location"]!.GetValue<string>()
                .ShouldBe("$message.header#/correlation-id");
        }
    }

    [Fact]
    public async Task TransformAsync_WithoutIncludeCorrelationId_AddsNoCorrelationId()
    {
        // Act
        var document = await GetDocumentAsync();

        // Assert
        document["components"]!["messages"]!["orders.order-placed"]!.AsObject().ContainsKey("correlationId").ShouldBeFalse();
    }

    [Fact]
    public async Task TransformAsync_WithExternalDocs_AddsExternalDocsToThatMessageOnly()
    {
        // Act
        var document = await GetDocumentAsync(wolverine =>
            wolverine.WithExternalDocs<OrderPlaced>("https://example.com/orders", "Order events"));

        // Assert
        var docs = document["components"]!["messages"]!["orders.order-placed"]!["externalDocs"]!;
        docs["url"]!.GetValue<string>().ShouldBe("https://example.com/orders");
        docs["description"]!.GetValue<string>().ShouldBe("Order events");
        document["components"]!["messages"]!["stock.stock-changed"]!.AsObject().ContainsKey("externalDocs").ShouldBeFalse();
    }

    [Fact]
    public async Task TransformAsync_WithMessageTraits_ReferencesRegisteredTrait()
    {
        // Act
        var document = await GetDocumentAsync(wolverine => wolverine.WithMessageTraits<OrderPlaced>("traced"));

        // Assert
        document["components"]!["messageTraits"]!["traced"].ShouldNotBeNull();
        document["components"]!["messages"]!["orders.order-placed"]!["traits"]!.AsArray().Single()!["$ref"]!.GetValue<string>()
            .ShouldBe("#/components/messageTraits/traced");
    }

    [Fact]
    public async Task TransformAsync_WithOperationTraits_ReferencesTraitOnThatMessagesOperations()
    {
        // Act
        var document = await GetDocumentAsync(wolverine => wolverine.WithOperationTraits<OrderPlaced>("audited"));

        // Assert
        document["components"]!["operationTraits"]!["audited"].ShouldNotBeNull();
        document["operations"]![SendOperation]!["traits"]!.AsArray().Single()!["$ref"]!.GetValue<string>()
            .ShouldBe("#/components/operationTraits/audited");
        document["operations"]![ReceiveOperation]!.AsObject().ContainsKey("traits").ShouldBeFalse();
    }

    [Fact]
    public async Task TransformAsync_RequireSecurity_ReferencesSchemeOnThatMessagesOperations()
    {
        // Act
        var document = await GetDocumentAsync(wolverine => wolverine.RequireSecurity<OrderPlaced>("apiKey"));

        // Assert
        document["components"]!["securitySchemes"]!["apiKey"].ShouldNotBeNull();
        document["operations"]![SendOperation]!["security"]!.AsArray().Single()!["$ref"]!.GetValue<string>()
            .ShouldBe("#/components/securitySchemes/apiKey");
        document["operations"]![ReceiveOperation]!.AsObject().ContainsKey("security").ShouldBeFalse();
    }

    [Fact]
    public async Task TransformAsync_AsyncApi2Document_WithMetadata_StillGeneratesDocument()
    {
        // Act
        var document = await GetDocumentAsync(
            wolverine => wolverine.IncludeCorrelationId().WithMessageTraits<OrderPlaced>("traced"),
            AsyncApiVersion.AsyncApi2_0);

        // Assert
        document["asyncapi"]!.GetValue<string>().ShouldBe("2.6.0");
    }

    [Fact]
    public void Metadata_NullOrBlankArguments_Throw()
    {
        // Arrange
        var options = new WolverineAsyncApiOptions();

        // Act & Assert
        Should.Throw<ArgumentException>(() => options.WithExternalDocs<OrderPlaced>(" "));
        Should.Throw<ArgumentNullException>(() => options.WithMessageTraits<OrderPlaced>(null!));
        Should.Throw<ArgumentException>(() => options.WithOperationTraits<OrderPlaced>(""));
        Should.Throw<ArgumentException>(() => options.RequireSecurity<OrderPlaced>(" "));
    }

    [Fact]
    public void Metadata_Methods_ReturnSameOptions()
    {
        // Arrange
        var options = new WolverineAsyncApiOptions();

        // Act & Assert
        options.IncludeCorrelationId().ShouldBeSameAs(options);
        options.WithExternalDocs<OrderPlaced>("https://example.com").ShouldBeSameAs(options);
        options.WithMessageTraits<OrderPlaced>("a").ShouldBeSameAs(options);
        options.WithOperationTraits<OrderPlaced>("a").ShouldBeSameAs(options);
        options.RequireSecurity<OrderPlaced>("a").ShouldBeSameAs(options);
    }

    private static Task<JsonNode> GetDocumentAsync(
        Action<WolverineAsyncApiOptions>? configure = null,
        AsyncApiVersion version = AsyncApiVersion.AsyncApi3_0) =>
        WolverineTestApp.GetDocumentAsync("messaging", options =>
        {
            options.AsyncApiVersion = version;
            options.AddServer("kafka", "localhost:9092", "kafka");
            options.AddMessageTrait("traced", new AsyncApiMessageTrait { ContentType = "application/json" });
            options.AddOperationTrait("audited", new AsyncApiOperationTrait { Summary = "Audited operation" });
            options.AddSecurityScheme("apiKey", new AsyncApiSecurityScheme { Type = SecuritySchemeType.ApiKey, In = ParameterLocation.User });
            options.AddWolverine(wolverine => configure?.Invoke(wolverine.IncludeSchemes("kafka")));
        });
}
