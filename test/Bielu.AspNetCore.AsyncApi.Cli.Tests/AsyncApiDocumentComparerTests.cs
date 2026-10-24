using Bielu.AspNetCore.AsyncApi.Cli.Commands;
using Bielu.Cli.Shared.Diff;
using ByteBard.AsyncAPI.Models;
using ByteBard.AsyncAPI.Readers;
using Shouldly;
using Xunit;

namespace Bielu.AspNetCore.AsyncApi.Cli.Tests;

/// <summary>
/// Unit tests for <see cref="AsyncApiDocumentComparer"/>: each kind of change, and whether it is breaking.
/// </summary>
public class AsyncApiDocumentComparerTests
{
    private const string Base = """
        {
          "asyncapi": "3.0.0",
          "info": { "title": "Shop", "version": "1.0.0" },
          "servers": {
            "kept": { "host": "a.example.com", "protocol": "kafka" },
            "dropped": { "host": "b.example.com", "protocol": "kafka" }
          },
          "channels": {
            "orders": { "address": "orders" },
            "moved": { "address": "legacy/moved" },
            "retired": { "address": "retired" }
          },
          "operations": {
            "placeOrder": { "action": "send", "channel": { "$ref": "#/channels/orders" } },
            "flipped": { "action": "send", "channel": { "$ref": "#/channels/orders" } },
            "rerouted": { "action": "receive", "channel": { "$ref": "#/channels/orders" } },
            "removedOp": { "action": "send", "channel": { "$ref": "#/channels/orders" } }
          },
          "components": {
            "messages": { "orderPlaced": { "payload": { "type": "object" } }, "legacyMessage": { "payload": { "type": "object" } } },
            "schemas": { "Order": { "type": "object" }, "LegacySchema": { "type": "object" } }
          }
        }
        """;

    private const string Head = """
        {
          "asyncapi": "3.0.0",
          "info": { "title": "Shop", "version": "2.0.0" },
          "servers": {
            "kept": { "host": "a.example.com", "protocol": "kafka" },
            "added": { "host": "c.example.com", "protocol": "kafka" }
          },
          "channels": {
            "orders": { "address": "orders" },
            "moved": { "address": "moved" },
            "shipments": { "address": "shipments" }
          },
          "operations": {
            "placeOrder": { "action": "send", "channel": { "$ref": "#/channels/orders" } },
            "flipped": { "action": "receive", "channel": { "$ref": "#/channels/orders" } },
            "rerouted": { "action": "receive", "channel": { "$ref": "#/channels/shipments" } },
            "addedOp": { "action": "send", "channel": { "$ref": "#/channels/shipments" } }
          },
          "components": {
            "messages": { "orderPlaced": { "payload": { "type": "object" } }, "newMessage": { "payload": { "type": "object" } } },
            "schemas": { "Order": { "type": "object" }, "NewSchema": { "type": "object" } }
          }
        }
        """;

    private static AsyncApiDocument Read(string json) => new AsyncApiStringReader().Read(json, out _);

    private static List<DocumentChange> Compare(string @base, string head) =>
        new AsyncApiDocumentComparer().Compare(Read(@base), Read(head)).ToList();

    [Fact]
    public void Compare_IdenticalDocuments_ReportsNothing()
    {
        // Act & Assert
        Compare(Base, Base).ShouldBeEmpty();
    }

    [Theory]
    [InlineData("servers/dropped", ChangeSeverity.Breaking)]
    [InlineData("servers/added", ChangeSeverity.NonBreaking)]
    [InlineData("channels/retired", ChangeSeverity.Breaking)]
    [InlineData("channels/shipments", ChangeSeverity.NonBreaking)]
    [InlineData("channels/moved/address", ChangeSeverity.Breaking)]
    [InlineData("operations/removedOp", ChangeSeverity.Breaking)]
    [InlineData("operations/addedOp", ChangeSeverity.NonBreaking)]
    [InlineData("operations/flipped/action", ChangeSeverity.Breaking)]
    [InlineData("operations/rerouted/channel", ChangeSeverity.Breaking)]
    [InlineData("components/messages/legacyMessage", ChangeSeverity.Breaking)]
    [InlineData("components/messages/newMessage", ChangeSeverity.NonBreaking)]
    [InlineData("components/schemas/LegacySchema", ChangeSeverity.Breaking)]
    [InlineData("components/schemas/NewSchema", ChangeSeverity.NonBreaking)]
    public void Compare_ChangedDocument_ReportsEachChangeWithItsSeverity(string path, ChangeSeverity severity)
    {
        // Act
        var changes = Compare(Base, Head);

        // Assert
        changes.ShouldContain(change => change.Path == path && change.Severity == severity);
    }

    [Fact]
    public void Compare_ChangedDocument_ReportsNothingForUnchangedItems()
    {
        // Act
        var changes = Compare(Base, Head);

        // Assert
        changes.Count.ShouldBe(13);
        changes.ShouldNotContain(change => change.Path.Contains("placeOrder") || change.Path.EndsWith("/orders") || change.Path.EndsWith("/kept"));
    }

    [Fact]
    public void Compare_DocumentsWithoutComponents_TreatsThemAsEmpty()
    {
        // Arrange
        const string bare = """{ "asyncapi": "3.0.0", "info": { "title": "Bare", "version": "1.0.0" } }""";

        // Act
        var changes = Compare(bare, Base);

        // Assert
        changes.ShouldContain(change => change.Path == "components/schemas/Order" && change.Severity == ChangeSeverity.NonBreaking);
        changes.ShouldAllBe(change => change.Severity == ChangeSeverity.NonBreaking);
    }
}
