using System.Text.Json.Nodes;
using Bielu.AspNetCore.AsyncApi.Wolverine.Tests.Fixtures;
using Shouldly;
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
        shipped["payload"]!["$ref"]!.GetValue<string>().ShouldBe("#/components/schemas/OrderShipped");
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
        var properties = document["components"]!["schemas"]!["ShapeProbe"]!["properties"]!;
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
}
