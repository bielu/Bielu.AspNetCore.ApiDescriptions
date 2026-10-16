using System.Text.Json.Nodes;
using Bielu.AspNetCore.AsyncApi.Wolverine.Tests.Fixtures;
using ByteBard.AsyncAPI;
using ByteBard.AsyncAPI.Models;
using Shouldly;
using Wolverine.Kafka;
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
    public async Task TransformAsync_IncludeCorrelationId_SignalRMessage_AddsNoHeaderCorrelationLocation()
    {
        // Act: Wolverine's SignalR transport sends a CloudEvents JSON wrapper, not a broker header, so
        // '$message.header#/correlation-id' never identifies anything in a SignalR message.
        var document = await WolverineTestApp.GetDocumentAsync("realtime", options =>
        {
            options.AddServer("signalr", "localhost", "signalr");
            options.AddWolverine(wolverine => wolverine.IncludeSchemes("signalr").IncludeCorrelationId());
        });

        // Assert
        foreach (var key in new[] { "notice", "orders.order-shipped", "shapes.shape-probe" })
        {
            document["components"]!["messages"]![key]!.AsObject().ContainsKey("correlationId").ShouldBeFalse();
        }
    }

    [Fact]
    public async Task TransformAsync_MessageAlsoDeclaredByAttributeWithSameKey_StillReceivesWolverineMetadata()
    {
        // Act: the attribute pipeline registers "orders.order-placed" before Wolverine's own routing loop
        // asks for the same message, so Wolverine's metadata must reach the component it finds already there.
        var document = await WolverineTestApp.GetDocumentAsync(AttributeChannels.MetadataSharedDocument, options =>
        {
            options.AddServer("kafka", "localhost:9092", "kafka");
            options.AddWolverine(wolverine => wolverine
                .IncludeSchemes("kafka")
                .IncludeCorrelationId()
                .WithExternalDocs<OrderPlaced>("https://example.com/orders"));
        });

        // Assert
        var message = document["components"]!["messages"]!["orders.order-placed"]!;
        message["correlationId"]!["location"]!.GetValue<string>().ShouldBe("$message.header#/correlation-id");
        message["externalDocs"]!["url"]!.GetValue<string>().ShouldBe("https://example.com/orders");
    }

    [Fact]
    public async Task TransformAsync_MessageRequestedByMultipleRoutes_ReferencesItsTraitOnlyOnce()
    {
        // Act: with replies included, OrderPlaced is requested once as the "orders" route's own message and
        // again as TrackOrder's reply, so ApplyMessageMetadata runs twice for the same component.
        var document = await WolverineTestApp.GetDocumentAsync(
            "messaging",
            options =>
            {
                options.AddServer("kafka", "localhost:9092", "kafka");
                options.AddWolverine(wolverine => wolverine
                    .IncludeSchemes("kafka")
                    .IncludeReplies()
                    .WithMessageTraits<OrderPlaced>("traced"));
                options.AddMessageTrait("traced", new AsyncApiMessageTrait { ContentType = "application/json" });
            },
            configureWolverine: opts => opts.ListenToKafkaTopic("tracking").DefaultIncomingMessage<TrackOrder>());

        // Assert
        document["components"]!["messages"]!["orders.order-placed"]!["traits"]!.AsArray().Single()!["$ref"]!
            .GetValue<string>().ShouldBe("#/components/messageTraits/traced");
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

    [Theory]
    [InlineData("orders")]
    [InlineData("/orders")]
    [InlineData("not a url")]
    public void WithExternalDocs_RelativeOrMalformedUrl_Throws(string url)
    {
        // Arrange
        var options = new WolverineAsyncApiOptions();

        // Act & Assert: AsyncAPI requires externalDocs.url to be absolute.
        Should.Throw<ArgumentException>(() => options.WithExternalDocs<OrderPlaced>(url));
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
