using Bielu.AspNetCore.AsyncApi.LikeC4.Tests.Fixtures;
using Shouldly;
using Xunit;

namespace Bielu.AspNetCore.AsyncApi.LikeC4.Tests.Unit;

public class LikeC4ModelCheckerTests
{
    private const string Orders = """
        {
          "asyncapi": "3.0.0",
          "info": { "title": "Orders", "version": "1" },
          "channels": { "created": { "address": "orders.created", "messages": { "m": { "name": "OrderCreated" } } } },
          "operations": { "publish": { "action": "send", "channel": { "$ref": "#/channels/created" } } }
        }
        """;

    private const string Inventory = """
        {
          "asyncapi": "3.0.0",
          "info": { "title": "Inventory", "version": "1" },
          "channels": { "created": { "address": "orders.created", "messages": { "m": { "name": "OrderCreated" } } } },
          "operations": { "receive": { "action": "receive", "channel": { "$ref": "#/channels/created" } } }
        }
        """;

    private static AsyncApiLikeC4Source[] Shop() => [new("orders", Orders), new("inventory", Inventory)];

    /// <summary>A model export with the given elements (<c>id → asyncapi or asyncapiChannel metadata</c>) and relations.</summary>
    private static string Model(string elements, string relations = "{}") =>
        $$"""{ "elements": {{elements}}, "relations": {{relations}} }""";

    private const string ServiceElements = """
        {
          "orders": { "title": "Orders", "metadata": { "asyncapi": "orders" } },
          "inventory": { "title": "Inventory", "metadata": { "asyncapi": "Inventory" } }
        }
        """;

    [Fact]
    public void Check_HandWrittenShopModel_FindsWhereItDisagreesWithTheDocuments()
    {
        // Arrange
        var model = ShopFixtures.Read("handwritten.likec4.json");

        // Act
        var findings = LikeC4ModelChecker.Check(model, ShopFixtures.Sources());

        // Assert
        findings.Select(finding => (finding.Severity, finding.Message, finding.Location)).ShouldBe(
        [
            (LikeC4CheckSeverity.Error, "Element 'billing' stands for AsyncAPI document 'billing', which is not one of the documents checked.", "element billing"),
            (LikeC4CheckSeverity.Error, "Element 'paymentsDone' stands for channel 'payments.done', which no AsyncAPI document declares.", "element paymentsDone"),
            (LikeC4CheckSeverity.Error, "The relationship 'Orders' -> 'Notifications' (notifies) has no AsyncAPI operation behind it: neither sends messages to a channel the other receives them from.", "relation " + RelationId(model, "shop.orders", "notifications")),
            (LikeC4CheckSeverity.Error, "Service 'inventory' sends StockLevelChanged to service 'notifications' through channel 'inventory.stock-changed', but the model has no relationship between them.", null),
        ]);
    }

    [Fact]
    public void Check_GeneratedModel_AgreesWithItsDocuments()
    {
        // Arrange: the generated model, exported by LikeC4, has the metadata the checker reads.
        var elements = """
            {
              "orders": { "title": "Orders", "metadata": { "asyncapi": "orders" } },
              "inventory": { "title": "Inventory", "metadata": { "asyncapi": "inventory" } },
              "ordersCreated": { "title": "orders.created", "metadata": { "asyncapiChannel": "orders.created" } }
            }
            """;
        var relations = """
            {
              "a": { "source": { "model": "orders" }, "target": { "model": "ordersCreated" } },
              "b": { "source": { "model": "ordersCreated" }, "target": { "model": "inventory" } },
              "c": { "source": { "model": "orders" }, "target": { "model": "inventory" } }
            }
            """;

        // Act
        var findings = LikeC4ModelChecker.Check(Model(elements, relations), Shop());

        // Assert
        findings.ShouldBeEmpty();
    }

    [Fact]
    public void Check_ServicesWithoutTheirFlow_ReportsTheMissingRelationship()
    {
        // Act
        var findings = LikeC4ModelChecker.Check(Model(ServiceElements), Shop());

        // Assert
        findings.ShouldHaveSingleItem().Message.ShouldBe(
            "Service 'orders' sends OrderCreated to service 'inventory' through channel 'orders.created', but the model has no relationship between them.");
    }

    [Fact]
    public void Check_FlowThroughAnOmittedChannelBesideAModelledOne_ReportsTheMissingRelationship()
    {
        // Arrange: orders sends to inventory through two channels; the model draws only the first.
        var orders = """
            {
              "asyncapi": "3.0.0",
              "info": { "title": "Orders", "version": "1" },
              "channels": {
                "created": { "address": "orders.created", "messages": { "m": { "name": "OrderCreated" } } },
                "cancelled": { "address": "orders.cancelled", "messages": { "m": { "name": "OrderCancelled" } } }
              },
              "operations": {
                "publishCreated": { "action": "send", "channel": { "$ref": "#/channels/created" } },
                "publishCancelled": { "action": "send", "channel": { "$ref": "#/channels/cancelled" } }
              }
            }
            """;
        var inventory = """
            {
              "asyncapi": "3.0.0",
              "info": { "title": "Inventory", "version": "1" },
              "channels": {
                "created": { "address": "orders.created", "messages": { "m": { "name": "OrderCreated" } } },
                "cancelled": { "address": "orders.cancelled", "messages": { "m": { "name": "OrderCancelled" } } }
              },
              "operations": {
                "onCreated": { "action": "receive", "channel": { "$ref": "#/channels/created" } },
                "onCancelled": { "action": "receive", "channel": { "$ref": "#/channels/cancelled" } }
              }
            }
            """;
        var elements = """
            {
              "orders": { "title": "Orders", "metadata": { "asyncapi": "orders" } },
              "inventory": { "title": "Inventory", "metadata": { "asyncapi": "inventory" } },
              "ordersCreated": { "title": "orders.created", "metadata": { "asyncapiChannel": "orders.created" } }
            }
            """;
        var relations = """
            {
              "a": { "source": { "model": "orders" }, "target": { "model": "ordersCreated" } },
              "b": { "source": { "model": "ordersCreated" }, "target": { "model": "inventory" } }
            }
            """;

        // Act
        var findings = LikeC4ModelChecker.Check(Model(elements, relations), [new("orders", orders), new("inventory", inventory)]);

        // Assert
        findings.ShouldHaveSingleItem().Message.ShouldBe(
            "Service 'orders' sends OrderCancelled to service 'inventory' through channel 'orders.cancelled', but the model has no relationship between them.");
    }

    [Fact]
    public void Check_ChannelDeclaredButUsedByNoOperation_IsKnown()
    {
        // Arrange: the generator draws a declared channel even when no operation uses it.
        var orders = """
            {
              "asyncapi": "3.0.0",
              "info": { "title": "Orders", "version": "1" },
              "channels": { "audit": { "address": "orders.audit" } }
            }
            """;
        var elements = """
            {
              "orders": { "title": "Orders", "metadata": { "asyncapi": "orders" } },
              "ordersAudit": { "title": "orders.audit", "metadata": { "asyncapiChannel": "orders.audit" } }
            }
            """;

        // Act
        var findings = LikeC4ModelChecker.Check(Model(elements), [new("orders", orders)]);

        // Assert
        findings.ShouldBeEmpty();
    }

    [Fact]
    public void Check_RelationshipToADeclaredChannelWithoutAnOperation_IsStale()
    {
        // Arrange
        var orders = """
            {
              "asyncapi": "3.0.0",
              "info": { "title": "Orders", "version": "1" },
              "channels": { "audit": { "address": "orders.audit" } }
            }
            """;
        var elements = """
            {
              "orders": { "title": "Orders", "metadata": { "asyncapi": "orders" } },
              "ordersAudit": { "title": "orders.audit", "metadata": { "asyncapiChannel": "orders.audit" } }
            }
            """;
        var relations = """{ "a": { "source": { "model": "orders" }, "target": { "model": "ordersAudit" } } }""";

        // Act
        var findings = LikeC4ModelChecker.Check(Model(elements, relations), [new("orders", orders)]);

        // Assert
        findings.ShouldHaveSingleItem().Message.ShouldBe(
            "The relationship 'Orders' -> 'orders.audit' has no AsyncAPI operation behind it: service 'orders' neither sends to nor receives from channel 'orders.audit'.");
    }

    [Fact]
    public void Check_FlowDrawnTheOtherWayRound_IsBacked()
    {
        // Arrange: drawn as a dependency, inventory on orders.
        var relations = """{ "r": { "source": "inventory", "target": "orders", "title": "depends on" } }""";

        // Act
        var findings = LikeC4ModelChecker.Check(Model(ServiceElements, relations), Shop());

        // Assert
        findings.ShouldBeEmpty();
    }

    [Fact]
    public void Check_ModelledChannelWithoutTheOperationsOnIt_ReportsEachMissingRelationship()
    {
        // Arrange
        var elements = """
            {
              "orders": { "title": "Orders", "metadata": { "asyncapi": "orders" } },
              "inventory": { "title": "Inventory", "metadata": { "asyncapi": "inventory" } },
              "topic": { "title": "orders.created", "metadata": { "asyncapiChannel": ["orders.created"] } }
            }
            """;

        // Act
        var findings = LikeC4ModelChecker.Check(Model(elements), Shop());

        // Assert
        findings.Select(finding => finding.Message).ShouldBe(
        [
            "Service 'orders' sends to channel 'orders.created' (operation 'publish'), but the model has no relationship between them.",
            "Service 'inventory' receives from channel 'orders.created' (operation 'receive'), but the model has no relationship between them.",
        ]);
    }

    [Fact]
    public void Check_RelationshipBetweenAServiceAndAChannelItDoesNotUse_IsStale()
    {
        // Arrange
        var other = """
            {
              "asyncapi": "3.0.0",
              "channels": { "x": { "address": "other" } },
              "operations": { "s": { "action": "send", "channel": { "$ref": "#/channels/x" } } }
            }
            """;
        var elements = """
            {
              "orders": { "title": "Orders", "metadata": { "asyncapi": "orders" } },
              "other": { "title": "Other", "metadata": { "asyncapiChannel": "other" } }
            }
            """;
        var relations = """{ "r": { "source": { "model": "other" }, "target": { "model": "orders" } } }""";

        // Act
        var findings = LikeC4ModelChecker.Check(Model(elements, relations), [new("orders", Orders), new("others", other)]);

        // Assert
        findings[0].Message.ShouldBe(
            "The relationship 'Other' -> 'Orders' has no AsyncAPI operation behind it: service 'orders' neither sends to nor receives from channel 'other'.");
        findings[0].Location.ShouldBe("relation r");
        findings[1].Severity.ShouldBe(LikeC4CheckSeverity.Warning);
        findings[1].Message.ShouldBe("No element stands for AsyncAPI document 'others': give one the metadata asyncapi 'others'.");
        findings[1].Location.ShouldBe("source others");
    }

    [Fact]
    public void Check_RelationshipsWithElementsStandingForNothing_AreNotChecked()
    {
        // Arrange
        var elements = """
            {
              "orders": { "title": "Orders", "metadata": { "asyncapi": "orders", "owner": 3 } },
              "inventory": { "title": "Inventory", "metadata": { "asyncapi": "inventory" } },
              "db": { },
              "a": { "metadata": { "asyncapiChannel": "orders.created" } },
              "b": { "metadata": { "asyncapiChannel": "orders.created" } }
            }
            """;
        var relations = """
            {
              "toDb": { "source": { "model": "orders" }, "target": { "model": "db" } },
              "self": { "source": { "model": "orders" }, "target": { "model": "orders" } },
              "channels": { "source": { "model": "a" }, "target": { "model": "b" } },
              "send": { "source": { "model": "orders" }, "target": { "model": "a" } },
              "receive": { "source": { "model": "b" }, "target": { "model": "inventory" } },
              "broken": { "source": { "model": "orders" } }
            }
            """;

        // Act
        var findings = LikeC4ModelChecker.Check(Model(elements, relations), Shop());

        // Assert
        findings.ShouldBeEmpty();
    }

    [Theory]
    [InlineData("{ not json", "not JSON")]
    [InlineData("[]", "no 'elements'")]
    [InlineData("{ \"relations\": {} }", "no 'elements'")]
    public void Check_ThrowsForContentThatIsNotALikeC4Export(string model, string reason)
    {
        // Act & Assert
        Should.Throw<FormatException>(() => LikeC4ModelChecker.Check(model, Shop())).Message.ShouldContain(reason);
    }

    [Fact]
    public void Check_ThrowsForSourceThatIsNotAnAsyncApiDocument()
    {
        // Act & Assert
        Should.Throw<FormatException>(() => LikeC4ModelChecker.Check(Model("{}"), [new("orders", "{}")]))
            .Message.ShouldStartWith("AsyncAPI document 'orders':");
    }

    [Fact]
    public void Check_ThrowsForDuplicateSourceNames()
    {
        // Act & Assert
        Should.Throw<ArgumentException>(() => LikeC4ModelChecker.Check(Model("{}"), [new("orders", Orders), new("ORDERS", Orders)]))
            .Message.ShouldContain("'orders'");
    }

    [Fact]
    public void Check_ThrowsForNullArguments()
    {
        // Act & Assert
        Should.Throw<ArgumentNullException>(() => LikeC4ModelChecker.Check(null!, Shop()));
        Should.Throw<ArgumentNullException>(() => LikeC4ModelChecker.Check(Model("{}"), null!));
    }

    private static string RelationId(string model, string source, string target)
    {
        var relations = System.Text.Json.Nodes.JsonNode.Parse(model)!["relations"]!.AsObject();
        return relations.First(pair =>
            pair.Value!["source"]!["model"]!.GetValue<string>() == source && pair.Value["target"]!["model"]!.GetValue<string>() == target).Key;
    }
}
