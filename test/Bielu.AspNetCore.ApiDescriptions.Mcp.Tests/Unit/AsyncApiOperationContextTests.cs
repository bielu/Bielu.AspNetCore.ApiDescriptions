using System.Text.Json.Nodes;
using Bielu.AspNetCore.ApiDescriptions.Mcp.Tools;
using Shouldly;
using Xunit;

namespace Bielu.AspNetCore.ApiDescriptions.Mcp.Tests.Unit;

public class AsyncApiOperationContextTests
{
    private static JsonObject Document3() => JsonNode.Parse("""
        {
          "asyncapi": "3.0.0",
          "defaultContentType": "application/json",
          "servers": {
            "broker": { "host": "broker:9092", "protocol": "kafka", "security": [ { "$ref": "#/components/securitySchemes/sasl" } ] },
            "other": { "host": "other:1883", "protocol": "mqtt" }
          },
          "channels": {
            "orders": {
              "address": "orders.{region}.{tenant}",
              "servers": [ { "$ref": "#/servers/broker" } ],
              "parameters": { "region": { "description": "The region." } },
              "messages": {
                "created": { "$ref": "#/components/messages/OrderCreated" },
                "deleted": { "payload": { "type": "string" } }
              },
              "bindings": { "kafka": { "topic": "orders" } }
            },
            "replies": { "address": "orders.replies", "messages": { "ack": { "payload": { "type": "boolean" } } } }
          },
          "operations": {
            "placeOrder": {
              "action": "receive",
              "channel": { "$ref": "#/channels/orders" },
              "security": [ { "$ref": "#/components/securitySchemes/token" } ],
              "bindings": { "kafka": { "groupId": "orders" } },
              "messages": [ { "$ref": "#/channels/orders/messages/created" } ],
              "reply": { "channel": { "$ref": "#/channels/replies" }, "messages": [ { "$ref": "#/channels/replies/messages/ack" } ] }
            },
            "bare": { "action": "send", "channel": { "$ref": "#/channels/replies" } }
          },
          "components": {
            "messages": {
              "OrderCreated": {
                "contentType": "application/avro",
                "headers": { "type": "object" },
                "payload": { "$ref": "#/components/schemas/Order" },
                "examples": [ { "payload": { "id": "1" } } ]
              }
            },
            "schemas": { "Order": { "type": "object", "properties": { "id": { "type": "string" } } } },
            "securitySchemes": { "sasl": { "type": "scramSha256" }, "token": { "type": "httpApiKey", "in": "user" } }
          }
        }
        """)!.AsObject();

    private static (JsonObject Description, IReadOnlyList<KeyValuePair<string, JsonNode>> References) Describe(JsonObject document, string id)
    {
        var operation = AsyncApiDocumentInspector.FindOperation(document, id)!;
        return AsyncApiOperationContext.Describe(document, new JsonObject { ["name"] = "d" }, new ApiOperationHandle("AsyncAPI", "d", id), operation);
    }

    [Fact]
    public void Describe_GathersTheOperationsContract()
    {
        // Act
        var (description, references) = Describe(Document3(), "placeOrder");

        // Assert
        description["action"]!.GetValue<string>().ShouldBe("receive");
        description["perspective"]!.GetValue<string>().ShouldContain("receives");
        description["channel"]!["address"]!.GetValue<string>().ShouldBe("orders.{region}.{tenant}");
        var server = description["servers"]!.AsArray().ShouldHaveSingleItem()!;
        server["name"]!.GetValue<string>().ShouldBe("broker");
        server["protocol"]!.GetValue<string>().ShouldBe("kafka");
        server["note"].ShouldBeNull();
        description["security"]!.AsArray().Select(s => (s!["scheme"]!.GetValue<string>(), s["appliesVia"]!.GetValue<string>())).ShouldBe(
        [
            ("#/components/securitySchemes/token", "operation"),
            ("#/components/securitySchemes/sasl", "server broker"),
        ]);
        var message = description["messages"]!.AsArray().ShouldHaveSingleItem()!;
        message["pointer"]!.GetValue<string>().ShouldBe("#/components/messages/OrderCreated");
        message["contentType"]!.GetValue<string>().ShouldBe("application/avro");
        message["payload"]!["$ref"]!.GetValue<string>().ShouldBe("#/components/schemas/Order");
        description["examples"]!.AsArray().ShouldHaveSingleItem()!["provenance"]!.GetValue<string>().ShouldBe("authored");

        var keys = references.Select(entry => entry.Key).ToList();
        keys.ShouldContain("#/channels/orders");
        keys.ShouldContain("#/servers/broker");
        keys.ShouldNotContain("#/servers/other");
        keys.ShouldContain("#/components/schemas/Order");
        keys.ShouldContain("#/components/securitySchemes/sasl");
        keys.ShouldContain("#/channels/replies/messages/ack");
        keys.ShouldBeUnique();
    }

    [Fact]
    public void Describe_LeavesOutTheChannelsOtherMessagesAndSaysSo()
    {
        // Act
        var (description, references) = Describe(Document3(), "placeOrder");

        // Assert
        var channel = references.Single(entry => entry.Key == "#/channels/orders").Value;
        channel["messages"]!.AsObject().Select(entry => entry.Key).ShouldBe(["created"]);
        description["omitted"]!.AsArray().ShouldHaveSingleItem()!["note"]!.GetValue<string>().ShouldContain("deleted");
    }

    [Fact]
    public void Describe_ListsWhatTheContractDoesNotSpecify()
    {
        // Act
        var placeOrder = Fields(Describe(Document3(), "placeOrder").Description);
        var bare = Fields(Describe(Document3(), "bare").Description);

        // Assert
        placeOrder.ShouldBe(["channel.parameters.tenant"]);
        bare.ShouldBe(["operation.bindings", "channel.bindings", "messages", "reply"], ignoreOrder: true);
    }

    [Fact]
    public void Describe_ChannelWithoutServers_AppliesEveryServerAndSaysSo()
    {
        // Act
        var (description, _) = Describe(Document3(), "bare");

        // Assert
        description["servers"]!.AsArray().Select(server => server!["name"]!.GetValue<string>()).ShouldBe(["broker", "other"]);
        description["servers"]!.AsArray().ShouldAllBe(server => server!["note"] != null);
    }

    [Fact]
    public void Describe_AsyncApi2_TrimsTheChannelsOperationsAndReadsRequirementObjects()
    {
        // Arrange
        var document = JsonNode.Parse("""
            {
              "asyncapi": "2.6.0",
              "servers": { "prod": { "url": "mqtt://broker", "protocol": "mqtt", "security": [ { "oauth": ["orders:write"] } ] } },
              "channels": {
                "orders/created": {
                  "servers": ["prod"],
                  "subscribe": { "operationId": "onCreated", "message": { "payload": { "type": "string" } } },
                  "publish": { "operationId": "create", "message": { "payload": { "type": "integer" } } }
                }
              },
              "components": { "securitySchemes": { "oauth": { "type": "oauth2" } } }
            }
            """)!.AsObject();

        // Act
        var (description, references) = Describe(document, "create");

        // Assert
        description["pointer"]!.GetValue<string>().ShouldBe("#/channels/orders~1created/publish");
        description["action"]!.GetValue<string>().ShouldBe("receive");
        description["servers"]!.AsArray().ShouldHaveSingleItem()!["url"]!.GetValue<string>().ShouldBe("mqtt://broker");
        var requirement = description["security"]!.AsArray().ShouldHaveSingleItem()!;
        requirement["scheme"]!.GetValue<string>().ShouldBe("#/components/securitySchemes/oauth");
        requirement["type"]!.GetValue<string>().ShouldBe("oauth2");
        requirement["scopes"]!.AsArray().Single()!.GetValue<string>().ShouldBe("orders:write");
        var channel = references.Single(entry => entry.Key == "#/channels/orders~1created").Value.AsObject();
        channel.ContainsKey("publish").ShouldBeFalse();
        channel.ContainsKey("subscribe").ShouldBeFalse();
        references.Select(entry => entry.Key).ShouldContain("#/components/securitySchemes/oauth");
        // 2.x has no replies, so their absence is not a gap.
        Fields(description).ShouldNotContain("reply");
    }

    [Fact]
    public void Page_SplitsReferencesWithinTheBudgetAndKeepsAnOversizedOneWhole()
    {
        // Arrange
        var references = new List<KeyValuePair<string, JsonNode>>
        {
            new("#/a", JsonValue.Create(new string('a', 40))),
            new("#/b", JsonValue.Create(new string('b', 40))),
            new("#/c", JsonValue.Create(new string('c', 400))),
        };

        // Act
        var (first, next) = GetApiOperationTool.Page(references, start: 0, budget: 100);
        var (second, afterSecond) = GetApiOperationTool.Page(references, next!.Value, budget: 100);

        // Assert
        first.Select(entry => entry.Key).ShouldBe(["#/a", "#/b"]);
        next.ShouldBe(2);
        second.Select(entry => entry.Key).ShouldBe(["#/c"]);
        afterSecond.ShouldBeNull();
    }

    [Fact]
    public void Cursor_RoundTripsAndRejectsOthers()
    {
        // Arrange
        var cursor = Cursor.Create("references", "sha256:a|b", 7);

        // Act & Assert
        Cursor.TryRead(cursor, "references", out var scope, out var offset).ShouldBeTrue();
        scope.ShouldBe("sha256:a|b");
        offset.ShouldBe(7);
        Cursor.TryRead(cursor, "find", out _, out _).ShouldBeFalse();
        Cursor.TryRead("%%%", "find", out _, out _).ShouldBeFalse();
        Cursor.TryRead(Convert.ToBase64String("find|x|-1"u8.ToArray()), "find", out _, out _).ShouldBeFalse();
    }

    private static List<string> Fields(JsonObject description) =>
        description["notSpecified"]!.AsArray().Select(entry => entry!["field"]!.GetValue<string>()).ToList();
}
