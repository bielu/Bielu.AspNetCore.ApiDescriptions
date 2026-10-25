using System.Text.Json.Nodes;
using Microsoft.Extensions.DependencyInjection;
using Shouldly;
using Xunit;

namespace Bielu.AspNetCore.ApiDescriptions.Mcp.Tests.Unit;

public class AsyncApiDocumentInspectorTests
{
    /// <summary>
    /// An operation whose message alias (<c>created</c>) differs from the component key (<c>OrderCreated</c>), an inline
    /// channel message, a channel key that needs escaping, and a recursive schema.
    /// </summary>
    private static JsonObject Orders() => JsonNode.Parse("""
        {
          "asyncapi": "3.1.0",
          "channels": {
            "orders/v1~beta": {
              "address": "orders.{region}",
              "messages": {
                "created": { "$ref": "#/components/messages/OrderCreated" },
                "cancelled": { "name": "OrderCancelled", "payload": { "type": "string" } }
              }
            }
          },
          "operations": {
            "onOrder": {
              "action": "receive",
              "channel": { "$ref": "#/channels/orders~1v1~0beta" },
              "summary": "Takes orders.",
              "messages": [
                { "$ref": "#/channels/orders~1v1~0beta/messages/created" },
                { "$ref": "#/channels/orders~1v1~0beta/messages/cancelled" }
              ]
            }
          },
          "components": {
            "messages": {
              "OrderCreated": { "payload": { "$ref": "#/components/schemas/Order" } }
            },
            "schemas": {
              "Order": { "type": "object", "properties": { "parent": { "$ref": "#/components/schemas/Order" }, "line": { "$ref": "#/components/schemas/Line" } } },
              "Line": { "type": "string" }
            }
          }
        }
        """)!.AsObject();

    [Fact]
    public void ListOperations_AsyncApi2_MapsSubscribeToSendAndPublishToReceive()
    {
        // Arrange
        var document = JsonNode.Parse("""
            {
              "asyncapi": "2.6.0",
              "channels": {
                "orders": {
                  "subscribe": { "operationId": "sendOrder", "summary": "Sends.", "message": { "$ref": "#/components/messages/order" } },
                  "publish": { "message": { "oneOf": [ { "$ref": "#/components/messages/a" }, { "name": "b" } ] } }
                }
              },
              "components": { "messages": { "order": {}, "a": {} } }
            }
            """)!.AsObject();

        // Act
        var operations = AsyncApiDocumentInspector.ListOperations(document);

        // Assert
        operations.Count.ShouldBe(2);
        operations[0]!["id"]!.GetValue<string>().ShouldBe("sendOrder");
        operations[0]!["action"]!.GetValue<string>().ShouldBe("send");
        operations[0]!["address"]!.GetValue<string>().ShouldBe("orders");
        Messages(operations[0]!).ShouldBe([("order", "#/components/messages/order")]);
        operations[1]!["id"]!.GetValue<string>().ShouldBe("publish orders");
        operations[1]!["action"]!.GetValue<string>().ShouldBe("receive");
        Messages(operations[1]!).ShouldBe([("a", "#/components/messages/a"), ("b", "#/channels/orders/publish/message/oneOf/1")]);
    }

    [Fact]
    public void ListOperations_AliasDiffersFromComponentKey_ReturnsTheCanonicalMessagePointer()
    {
        // Act
        var operation = AsyncApiDocumentInspector.ListOperations(Orders()).ShouldHaveSingleItem()!;

        // Assert
        operation["channel"]!.GetValue<string>().ShouldBe("orders/v1~beta");
        operation["address"]!.GetValue<string>().ShouldBe("orders.{region}");
        Messages(operation).ShouldBe(
        [
            ("created", "#/components/messages/OrderCreated"),
            ("cancelled", "#/channels/orders~1v1~0beta/messages/cancelled"),
        ]);
    }

    [Theory]
    [InlineData("created")]
    [InlineData("OrderCreated")]
    [InlineData("orderCREATED")]
    [InlineData("#/components/messages/OrderCreated")]
    [InlineData("#/channels/orders~1v1~0beta/messages/created")]
    [InlineData("#/channels/orders%7E1v1%7E0beta/messages/created")]
    public void GetMessage_FindsAMessageByAnyNameItIsKnownBy(string name)
    {
        // Act
        var (message, error) = AsyncApiDocumentInspector.GetMessage(Orders(), name);

        // Assert
        error.ShouldBeNull();
        message!["pointer"]!.GetValue<string>().ShouldBe("#/components/messages/OrderCreated");
    }

    [Fact]
    public void GetMessage_InlineChannelMessage_IsFoundByAliasAndPointer()
    {
        // Act
        var byAlias = AsyncApiDocumentInspector.GetMessage(Orders(), "cancelled").Message!;
        var byPointer = AsyncApiDocumentInspector.GetMessage(Orders(), "#/channels/orders~1v1~0beta/messages/cancelled").Message!;

        // Assert
        byAlias["pointer"]!.GetValue<string>().ShouldBe("#/channels/orders~1v1~0beta/messages/cancelled");
        byAlias["message"]!["name"]!.GetValue<string>().ShouldBe("OrderCancelled");
        byPointer["pointer"]!.GetValue<string>().ShouldBe(byAlias["pointer"]!.GetValue<string>());
    }

    [Fact]
    public void GetMessage_KeepsReferencesAndCollectsARecursiveSchemaOnce()
    {
        // Arrange
        var document = Orders();

        // Act
        var message = AsyncApiDocumentInspector.GetMessage(document, "created").Message!;

        // Assert
        message["message"]!["payload"]!["$ref"]!.GetValue<string>().ShouldBe("#/components/schemas/Order");
        message["references"]!.AsObject().Select(entry => entry.Key)
            .ShouldBe(["#/components/schemas/Order", "#/components/schemas/Line"]);
        message["references"]!["#/components/schemas/Order"]!["properties"]!["parent"]!["$ref"]!.GetValue<string>()
            .ShouldBe("#/components/schemas/Order");
        message["unresolved"].ShouldBeNull();
        // The document itself is left as it was.
        document["components"]!["messages"]!["OrderCreated"]!["payload"]!["$ref"]!.GetValue<string>().ShouldBe("#/components/schemas/Order");
    }

    [Fact]
    public void GetMessage_ReportsReferencesItCannotFollow()
    {
        // Arrange
        var document = JsonNode.Parse("""
            {
              "asyncapi": "3.0.0",
              "components": { "messages": { "m": {
                "payload": { "$ref": "https://example.com/order.json" },
                "headers": { "$ref": "#/components/schemas/missing" }
              } } }
            }
            """)!.AsObject();

        // Act
        var message = AsyncApiDocumentInspector.GetMessage(document, "m").Message!;

        // Assert
        message["unresolved"]!.AsArray().Select(entry => entry!["ref"]!.GetValue<string>())
            .ShouldBe(["https://example.com/order.json", "#/components/schemas/missing"]);
    }

    [Fact]
    public void GetMessage_AmbiguousName_ListsThePointersToChooseFrom()
    {
        // Arrange
        var document = JsonNode.Parse("""
            {
              "asyncapi": "3.0.0",
              "channels": {
                "a": { "messages": { "event": { "payload": { "type": "string" } } } },
                "b": { "messages": { "event": { "payload": { "type": "integer" } } } }
              }
            }
            """)!.AsObject();

        // Act
        var (message, error) = AsyncApiDocumentInspector.GetMessage(document, "event");

        // Assert
        message.ShouldBeNull();
        error!.ShouldContain("#/channels/a/messages/event");
        error!.ShouldContain("#/channels/b/messages/event");
    }

    [Theory]
    [InlineData("nope", "Its messages are")]
    [InlineData("#/components/messages/nope", "does not exist")]
    public void GetMessage_Missing_SaysWhy(string name, string expected)
    {
        // Act
        var (message, error) = AsyncApiDocumentInspector.GetMessage(Orders(), name);

        // Assert
        message.ShouldBeNull();
        error!.ShouldContain(expected);
    }

    [Fact]
    public void GetMessage_ReferenceCycle_IsAnErrorRatherThanALoop()
    {
        // Arrange
        var document = JsonNode.Parse("""
            { "asyncapi": "3.0.0", "components": { "messages": {
              "a": { "$ref": "#/components/messages/b" },
              "b": { "$ref": "#/components/messages/a" }
            } } }
            """)!.AsObject();

        // Act
        var (_, error) = AsyncApiDocumentInspector.GetMessage(document, "a");

        // Assert
        error!.ShouldContain("cycle");
    }

    [Fact]
    public void GetServers_ChannelWithoutServers_AppliesEveryServer()
    {
        // Arrange
        var document = JsonNode.Parse("""
            {
              "asyncapi": "3.0.0",
              "servers": { "one": { "host": "a", "protocol": "kafka" }, "two": { "host": "b", "protocol": "mqtt" } },
              "channels": {
                "open": { "address": "open" },
                "pinned": { "address": "pinned", "servers": [ { "$ref": "#/servers/two" } ] }
              },
              "operations": {
                "o": { "action": "send", "channel": { "$ref": "#/channels/open" } },
                "p": { "action": "send", "channel": { "$ref": "#/channels/pinned" } }
              }
            }
            """)!.AsObject();

        // Act
        var open = AsyncApiDocumentInspector.GetServers(document, AsyncApiDocumentInspector.FindOperation(document, "o")!, out var openImplicit);
        var pinned = AsyncApiDocumentInspector.GetServers(document, AsyncApiDocumentInspector.FindOperation(document, "P")!, out var pinnedImplicit);

        // Assert
        open.Select(server => server.Name).ShouldBe(["one", "two"]);
        openImplicit.ShouldBeTrue();
        pinned.ShouldHaveSingleItem().Pointer.ShouldBe("#/servers/two");
        pinnedImplicit.ShouldBeFalse();
    }

    [Fact]
    public void FindOperation_Unknown_ReturnsNull()
    {
        // Act & Assert
        AsyncApiDocumentInspector.FindOperation(Orders(), "nope").ShouldBeNull();
    }

    [Fact]
    public void ApiDescriptionDocument_BuildsItsUriAndMediaType()
    {
        // Act
        var document = new ApiDescriptionDocument("Custom", "my doc");

        // Assert
        document.Uri.ShouldBe("apidescription://custom/my%20doc");
        document.MimeType.ShouldBe("application/json");
    }

    [Fact]
    public void WithApiDescriptions_ThrowsForNullBuilder()
    {
        // Act & Assert
        Should.Throw<ArgumentNullException>(() => McpServerBuilderApiDescriptionsExtensions.WithApiDescriptions(null!));
    }

    private static (string Name, string Ref)[] Messages(JsonNode operation) =>
        operation["messages"]!.AsArray()
            .Select(message => (message!["name"]!.GetValue<string>(), message["ref"]!.GetValue<string>()))
            .ToArray();
}
