using Bielu.AspNetCore.AsyncApi.LikeC4.Internal;
using Bielu.AspNetCore.AsyncApi.LikeC4.Tests.Fixtures;
using Shouldly;
using Xunit;

namespace Bielu.AspNetCore.AsyncApi.LikeC4.Tests.Unit;

public class AsyncApiDocumentReaderTests
{
    [Fact]
    public void Read_Version3_ReadsChannelsAndOperationsThroughReferences()
    {
        // Act
        var service = AsyncApiDocumentReader.Read(ShopFixtures.Read("orders.asyncapi.json"));

        // Assert
        service.Title.ShouldBe("Orders API");
        service.Version.ShouldBe("1.2.0");
        service.Channels.Select(channel => channel.Address).ShouldBe(["orders.created", "inventory.reserved"]);
        service.Channels[0].Protocols.ShouldBe(["kafka"]);
        service.Channels[0].Messages.ShouldBe(["OrderCreated"]);
        service.Operations.Select(operation => (operation.Key, operation.Action, operation.ChannelKey)).ShouldBe(
        [
            ("publishOrderCreated", AsyncApiOperationAction.Send, "orderCreated"),
            ("onInventoryReserved", AsyncApiOperationAction.Receive, "inventoryReserved"),
        ]);
    }

    [Fact]
    public void Read_Version3OperationWithoutMessages_TakesItsChannelsMessages()
    {
        // Act
        var service = AsyncApiDocumentReader.Read(ShopFixtures.Read("orders.asyncapi.json"));

        // Assert
        service.Operations[1].Messages.ShouldBe(["InventoryReserved"]);
    }

    [Fact]
    public void Read_Version2_TurnsPublishIntoReceiveAndSubscribeIntoSend()
    {
        // Act
        var service = AsyncApiDocumentReader.Read(ShopFixtures.Read("notifications.asyncapi.yaml"));

        // Assert
        service.Operations.Select(operation => (operation.Key, operation.Action)).ShouldBe(
        [
            ("onStockLevelChanged", AsyncApiOperationAction.Receive),
            ("sendInventoryAlert", AsyncApiOperationAction.Send),
        ]);
        service.Operations[1].Messages.ShouldBe(["Low stock", "OutOfStock"]);
        service.Channels[1].Protocols.ShouldBe(["wss"]);
    }

    [Fact]
    public void Read_Version2OperationWithoutOperationId_IsKeyedByChannelAndField()
    {
        // Arrange
        var document = """{ "asyncapi": "2.6.0", "channels": { "a/b": { "subscribe": { "message": { "name": "M" } } } } }""";

        // Act
        var service = AsyncApiDocumentReader.Read(document);

        // Assert
        service.Operations.ShouldHaveSingleItem().Key.ShouldBe("a/b/subscribe");
        service.Channels.ShouldHaveSingleItem().Protocols.ShouldBeEmpty();
    }

    [Fact]
    public void Read_ChannelOnNoNamedServer_IsOnEveryServer()
    {
        // Arrange
        var document = """
            {
              "asyncapi": "3.0.0",
              "servers": { "a": { "protocol": "mqtt" }, "b": { "protocol": "amqp" }, "c": { "protocol": "MQTT" } },
              "channels": { "c": { "address": "x" } }
            }
            """;

        // Act
        var service = AsyncApiDocumentReader.Read(document);

        // Assert
        service.Channels.ShouldHaveSingleItem().Protocols.ShouldBe(["amqp", "mqtt"]);
    }

    [Fact]
    public void Read_SkipsOperationsItCannotPlace()
    {
        // Arrange
        var document = """
            {
              "asyncapi": "3.0.0",
              "channels": { "c": { "address": "x" }, "loop": { "$ref": "#/channels/loop" } },
              "operations": {
                "unknownAction": { "action": "publish", "channel": { "$ref": "#/channels/c" } },
                "noChannel": { "action": "send" },
                "componentChannel": { "action": "send", "channel": { "$ref": "#/components/channels/c" } },
                "nestedPointer": { "action": "send", "channel": { "$ref": "#/channels/c/messages" } },
                "external": { "$ref": "other.json#/operations/x" },
                "fine": { "action": "send", "channel": { "$ref": "#/channels/c" } }
              }
            }
            """;

        // Act
        var service = AsyncApiDocumentReader.Read(document);

        // Assert
        service.Operations.ShouldHaveSingleItem().Key.ShouldBe("fine");
        service.Channels.ShouldHaveSingleItem().Key.ShouldBe("c");
    }

    [Fact]
    public void Read_ChannelKeyWithEscapedSlash_IsFoundThroughItsReference()
    {
        // Arrange
        var document = """
            {
              "asyncapi": "3.0.0",
              "channels": { "a/b~c": { "address": "a/b" } },
              "operations": { "s": { "action": "send", "channel": { "$ref": "#/channels/a~1b~0c" } } }
            }
            """;

        // Act
        var service = AsyncApiDocumentReader.Read(document);

        // Assert
        service.Operations.ShouldHaveSingleItem().ChannelKey.ShouldBe("a/b~c");
    }

    [Fact]
    public void Read_MessageWithoutNameOrTitle_IsNamedByItsKey()
    {
        // Arrange
        var document = """
            { "asyncapi": "3.0.0", "channels": { "c": { "address": "x", "messages": { "inline": { "payload": {} } } } } }
            """;

        // Act
        var service = AsyncApiDocumentReader.Read(document);

        // Assert
        service.Channels.ShouldHaveSingleItem().Messages.ShouldBe(["inline"]);
    }
}
