using System.Text.Json.Nodes;
using Bielu.AspNetCore.AsyncApi.Services;
using Bielu.AspNetCore.AsyncApi.Wolverine.Tests.Fixtures;
using Shouldly;
using Wolverine.Kafka;
using Xunit;

namespace Bielu.AspNetCore.AsyncApi.Wolverine.Tests.Integration;

public class WolverineDocumentTransformerTests
{
    private static Task<JsonNode> GetSignalRDocumentAsync() =>
        WolverineTestApp.GetDocumentAsync("realtime", options =>
        {
            options.AddServer("signalr", "localhost", "signalr");
            options.AddWolverine(wolverine => wolverine.IncludeSchemes("signalr"));
        });

    private static Task<JsonNode> GetKafkaDocumentAsync() =>
        WolverineTestApp.GetDocumentAsync("messaging", options =>
        {
            options.AddServer("kafka", "localhost:9092", "kafka");
            options.AddWolverine(wolverine => wolverine.IncludeSchemes("kafka"));
        });

    [Fact]
    public async Task TransformAsync_SignalRRoute_AddsHubChannelAtMappedPath()
    {
        // Act
        var document = await GetSignalRDocumentAsync();

        // Assert
        var channel = document["channels"]!["hubstest"]!;
        channel["address"]!.GetValue<string>().ShouldBe(WolverineTestApp.HubPath);
        channel["bindings"]!["signalr"]!["hub"]!.GetValue<string>().ShouldBe(WolverineTestApp.HubPath);
        channel["servers"]!.AsArray().Single()!["$ref"]!.GetValue<string>().ShouldBe("#/servers/signalr");
    }

    [Fact]
    public async Task TransformAsync_SignalRRoute_NamesMessagesByWolverineMessageTypeName()
    {
        // Act
        var document = await GetSignalRDocumentAsync();

        // Assert
        var messages = document["components"]!["messages"]!.AsObject();
        messages.Select(m => m.Key).ShouldBe(
            [
                "notice",
                "orders.order-shipped",
                "shapes.shape-probe",
            ],
            ignoreOrder: true);

        var shipped = messages["orders.order-shipped"]!;
        shipped["name"]!.GetValue<string>().ShouldBe("orders.order-shipped");
        shipped["title"]!.GetValue<string>().ShouldBe(nameof(OrderShipped));
        shipped["payload"]!["$ref"]!.GetValue<string>().ShouldBe("#/components/schemas/orderShipped");
    }

    [Fact]
    public async Task TransformAsync_SignalRRoute_AddsSendOperationTargetingReceiveMessage()
    {
        // Act
        var document = await GetSignalRDocumentAsync();

        // Assert
        var operation = document["operations"]!["send.orders.order-shipped"]!;
        operation["action"]!.GetValue<string>().ShouldBe("send");
        operation["channel"]!["$ref"]!.GetValue<string>().ShouldBe("#/channels/hubstest");
        operation["messages"]!.AsArray().Single()!["$ref"]!.GetValue<string>()
            .ShouldBe("#/channels/hubstest/messages/orders.order-shipped");

        var binding = operation["bindings"]!["signalr"]!;
        binding["target"]!.GetValue<string>().ShouldBe("ReceiveMessage");
        binding["direction"]!.GetValue<string>().ShouldBe("serverToClient");
    }

    [Fact]
    public async Task TransformAsync_PayloadSchema_KeepsNullabilityAndNumericTypes()
    {
        // Act
        var document = await GetSignalRDocumentAsync();

        // Assert: under ASP.NET's web JSON defaults (numbers readable from strings)
        var properties = document["components"]!["schemas"]!["shapeProbe"]!["properties"]!;
        properties["count"]!["type"]!.GetValue<string>().ShouldBe("integer");
        properties["maybeCount"]!["type"]!.AsArray().Select(t => t!.GetValue<string>()).ShouldBe(["null", "integer"], ignoreOrder: true);
        properties["note"]!["type"]!.AsArray().Select(t => t!.GetValue<string>()).ShouldBe(["null", "string"], ignoreOrder: true);
        properties["count"]!.AsObject().ContainsKey("pattern").ShouldBeFalse();
    }

    [Fact]
    public async Task TransformAsync_LocalOnlyMessages_AreNotDocumented()
    {
        // Act
        var document = await GetSignalRDocumentAsync();

        // Assert
        document["components"]!["messages"]!.AsObject().Select(m => m.Key).ShouldNotContain("shiporder");
        document["channels"]!.AsObject().Count.ShouldBe(1);
    }

    [Fact]
    public async Task TransformAsync_KafkaPublishRoute_AddsSendOperationOnTopic()
    {
        // Act
        var document = await GetKafkaDocumentAsync();

        // Assert
        var channel = document["channels"]!["orders"]!;
        channel["address"]!.GetValue<string>().ShouldBe("orders");
        channel["bindings"]!["kafka"]!["topic"]!.GetValue<string>().ShouldBe("orders");

        var operation = document["operations"]!["send.orders.order-placed"]!;
        operation["action"]!.GetValue<string>().ShouldBe("send");
        operation["channel"]!["$ref"]!.GetValue<string>().ShouldBe("#/channels/orders");
    }

    [Fact]
    public async Task TransformAsync_KafkaListenerWithDefaultIncomingMessage_AddsReceiveOperation()
    {
        // Act
        var document = await GetKafkaDocumentAsync();

        // Assert
        var operation = document["operations"]!["receive.stock.stock-changed"]!;
        operation["action"]!.GetValue<string>().ShouldBe("receive");
        operation["channel"]!["$ref"]!.GetValue<string>().ShouldBe("#/channels/stock");
        document["components"]!["messages"]!["stock.stock-changed"]!["summary"]!.GetValue<string>()
            .ShouldBe("Read from Kafka, sent by a producer that isn't Wolverine.");
    }

    [Fact]
    public async Task TransformAsync_KafkaDocument_LeavesSignalRMessagesOut()
    {
        // Act
        var document = await GetKafkaDocumentAsync();

        // Assert
        document["components"]!["messages"]!.AsObject().ContainsKey("orders.order-shipped").ShouldBeFalse();
    }

    [Fact]
    public async Task TransformAsync_MapChannel_OverridesDerivedAddress()
    {
        // Act
        var document = await WolverineTestApp.GetDocumentAsync("realtime", options =>
            options.AddWolverine(wolverine => wolverine
                .IncludeSchemes("signalr")
                .MapChannel(new Uri("signalr://wolverine/"), "/custom")),
            mapHub: false);

        // Assert
        document["channels"]!["custom"]!["address"]!.GetValue<string>().ShouldBe("/custom");
    }

    [Fact]
    public async Task TransformAsync_SignalRWithoutMappedHub_Throws()
    {
        // Act & Assert
        await Should.ThrowAsync<Exception>(() => WolverineTestApp.GetDocumentAsync("realtime", options =>
            options.AddWolverine(wolverine => wolverine.IncludeSchemes("signalr")),
            mapHub: false));
    }

    [Fact]
    public async Task TransformAsync_MessageRoutedToThreeEndpointsWithSameAddress_DocumentsEveryRoute()
    {
        // Act: three distinct topics whose channels all resolve to the address "orders"
        var document = await WolverineTestApp.GetDocumentAsync("messaging", options =>
                options.AddWolverine(wolverine => wolverine
                    .IncludeSchemes("kafka")
                    .MapChannel(new Uri("kafka://topic/orders-audit"), "orders")
                    .MapChannel(new Uri("kafka://topic/orders-archive"), "orders")),
            configureWolverine: opts =>
            {
                opts.PublishMessage<OrderPlaced>().ToKafkaTopic("orders-audit");
                opts.PublishMessage<OrderPlaced>().ToKafkaTopic("orders-archive");
            });

        // Assert
        var sends = document["operations"]!.AsObject()
            .Where(o => o.Value!["messages"]!.AsArray().Single()!["$ref"]!.GetValue<string>().EndsWith("/orders.order-placed", StringComparison.Ordinal))
            .Select(o => o.Value!["channel"]!["$ref"]!.GetValue<string>())
            .ToList();
        sends.Count.ShouldBe(3);
        sends.Distinct().Count().ShouldBe(3);

        var channels = document["channels"]!.AsObject();
        channels.Count(c => c.Value!["address"]!.GetValue<string>() == "orders").ShouldBe(3);
    }

    [Fact]
    public async Task TransformAsync_CustomSchemaReferenceId_PayloadReferencesRegisteredSchema()
    {
        // Act
        var document = await WolverineTestApp.GetDocumentAsync("messaging", options =>
        {
            options.CreateSchemaReferenceId = typeInfo => typeInfo.Type == typeof(OrderPlaced)
                ? "PlacedOrderV1"
                : AsyncApiOptions.CreateDefaultSchemaReferenceId(typeInfo);
            options.AddWolverine(wolverine => wolverine.IncludeSchemes("kafka"));
        });

        // Assert
        document["components"]!["messages"]!["orders.order-placed"]!["payload"]!["$ref"]!.GetValue<string>()
            .ShouldBe("#/components/schemas/placedOrderV1");
        document["components"]!["schemas"]!["placedOrderV1"]!["properties"]!.AsObject().ContainsKey("orderId").ShouldBeTrue();
    }

    [Fact]
    public async Task TransformAsync_SchemaReferenceIdNull_InlinesPayload()
    {
        // Act
        var document = await WolverineTestApp.GetDocumentAsync("messaging", options =>
        {
            options.CreateSchemaReferenceId = _ => null;
            options.AddWolverine(wolverine => wolverine.IncludeSchemes("kafka"));
        });

        // Assert
        var payload = document["components"]!["messages"]!["orders.order-placed"]!["payload"]!.AsObject();
        payload.ContainsKey("$ref").ShouldBeFalse();
        payload["properties"]!.AsObject().ContainsKey("orderId").ShouldBeTrue();
    }

    [Fact]
    public async Task TransformAsync_SameNamedMessageTypes_ThrowsInsteadOfSharingSchema()
    {
        // Act & Assert
        var exception = await Should.ThrowAsync<InvalidOperationException>(() => GetSameNamedDocumentAsync(_ => { }));
        exception.Message.ShouldContain(nameof(AsyncApiOptions.CreateSchemaReferenceId));
    }

    [Fact]
    public async Task TransformAsync_SameNamedMessageTypesWithDistinctSchemaIds_EachPayloadDescribesItsOwnType()
    {
        // Act
        var document = await GetSameNamedDocumentAsync(options =>
            options.CreateSchemaReferenceId = typeInfo => typeInfo.Type.FullName);

        // Assert
        var messages = document["components"]!["messages"]!;
        var schemas = document["components"]!["schemas"]!;
        PayloadProperties(typeof(Fixtures.Orders.Created)).ShouldContain("orderId");
        PayloadProperties(typeof(Fixtures.Orders.Created)).ShouldNotContain("invoiceId");
        PayloadProperties(typeof(Fixtures.Billing.Created)).ShouldContain("invoiceId");
        PayloadProperties(typeof(Fixtures.Billing.Created)).ShouldNotContain("orderId");

        IEnumerable<string> PayloadProperties(Type messageType)
        {
            var schemaRef = messages[messageType.FullName!]!["payload"]!["$ref"]!.GetValue<string>();
            return schemas[schemaRef["#/components/schemas/".Length..]]!["properties"]!.AsObject().Select(p => p.Key);
        }
    }

    [Fact]
    public async Task TransformAsync_MessageClashingWithAttributePayloadSchemaId_Throws()
    {
        // Act & Assert: the attribute pipeline already registered Billing.Created as "created"
        var exception = await Should.ThrowAsync<InvalidOperationException>(() =>
            GetDocumentWithOrdersCreatedRouteAsync(AttributeChannels.ClashingDocument));
        exception.Message.ShouldContain(typeof(Fixtures.Billing.Created).FullName!);
        exception.Message.ShouldContain(typeof(Fixtures.Orders.Created).FullName!);
    }

    [Fact]
    public async Task TransformAsync_MessageAlsoDeclaredByAttribute_SharesItsSchema()
    {
        // Act
        var document = await GetDocumentWithOrdersCreatedRouteAsync(AttributeChannels.SharedDocument);

        // Assert
        var messages = document["components"]!["messages"]!;
        messages[typeof(Fixtures.Orders.Created).FullName!]!["payload"]!["$ref"]!.GetValue<string>()
            .ShouldBe("#/components/schemas/created");
        messages["orderCreated"]!["payload"]!["$ref"]!.GetValue<string>().ShouldBe("#/components/schemas/created");
        document["components"]!["schemas"]!["created"]!["properties"]!.AsObject().ContainsKey("orderId").ShouldBeTrue();
    }

    private static Task<JsonNode> GetDocumentWithOrdersCreatedRouteAsync(string documentName) =>
        WolverineTestApp.GetDocumentAsync(documentName,
            options => options.AddWolverine(wolverine => wolverine.IncludeSchemes("kafka")),
            configureWolverine: opts => opts.PublishMessage<Fixtures.Orders.Created>().ToKafkaTopic("orders-created"));

    private static Task<JsonNode> GetSameNamedDocumentAsync(Action<AsyncApiOptions> configure) =>
        WolverineTestApp.GetDocumentAsync("messaging", options =>
            {
                configure(options);
                options.AddWolverine(wolverine => wolverine.IncludeSchemes("kafka"));
            },
            configureWolverine: opts =>
            {
                opts.PublishMessage<Fixtures.Orders.Created>().ToKafkaTopic("orders-created");
                opts.PublishMessage<Fixtures.Billing.Created>().ToKafkaTopic("invoices-created");
            });
}
