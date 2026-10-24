using System.Text;
using System.Text.Json.Nodes;
using Bielu.AspNetCore.XRegistry.Internal;
using Microsoft.Extensions.Primitives;
using Shouldly;
using Xunit;

namespace Bielu.AspNetCore.XRegistry.Tests.Unit;

public class AsyncApiRegistryReaderTests
{
    [Fact]
    public void ReadGroup_AsyncApi3_ReadsMessagesAndTheirSchemaFormats()
    {
        // Arrange
        const string json = """
            {
              "asyncapi": "3.1.0",
              "info": { "title": "Orders", "description": "Order events" },
              "defaultContentType": "application/json",
              "components": {
                "messages": {
                  "placed": { "title": "Placed", "summary": "An order was placed.", "payload": { "$ref": "#/components/schemas/placed" } },
                  "avro": { "payload": { "schemaFormat": "application/vnd.apache.avro;version=1.11.1", "schema": { "type": "record", "name": "A", "fields": [] } } },
                  "proto": { "contentType": "application/protobuf", "payload": { "schemaFormat": "application/vnd.google.protobuf;version=3", "schema": "syntax = \"proto3\";" } },
                  "empty": { "name": "empty" },
                  "shared": { "$ref": "#/components/messages/placed" }
                },
                "schemas": { "placed": { "type": "object", "properties": { "id": { "type": "string" } } } }
              }
            }
            """;

        // Act
        var group = AsyncApiRegistryReader.ReadGroup("orders", json);

        // Assert
        group.Name.ShouldBe("Orders");
        group.Description.ShouldBe("Order events");
        group.Messages.Select(m => m.Id).ShouldBe(["placed", "avro", "proto", "empty"]);

        var placed = group.FindMessage("placed")!;
        placed.Name.ShouldBe("Placed");
        placed.Description.ShouldBe("An order was placed.");
        placed.DataContentType.ShouldBe("application/json");
        placed.Schema!.Format.ShouldBe("JsonSchema/draft-07");
        placed.Schema.JsonDocument!["properties"]!["id"]!["type"]!.GetValue<string>().ShouldBe("string");

        var avro = group.FindSchema("avro")!;
        avro.Format.ShouldBe("Avro/1.11.1");
        avro.JsonDocument!["type"]!.GetValue<string>().ShouldBe("record");

        var proto = group.FindSchema("proto")!;
        proto.Format.ShouldBe("Protobuf/3");
        proto.JsonDocument.ShouldBeNull();
        Encoding.UTF8.GetString(proto.Document).ShouldBe("syntax = \"proto3\";");
        group.FindMessage("proto")!.DataContentType.ShouldBe("application/protobuf");

        group.FindMessage("empty")!.Schema.ShouldBeNull();
        group.Schemas.Count.ShouldBe(3);
    }

    [Fact]
    public void ReadGroup_AsyncApi2_ReadsTheSchemaFormatFromTheMessage()
    {
        // Arrange
        const string json = """
            {
              "asyncapi": "2.6.0",
              "info": { "title": "Orders" },
              "components": {
                "messages": {
                  "avro": { "schemaFormat": "application/vnd.apache.avro;version=1.9.0", "payload": { "type": "record", "name": "A", "fields": [] } },
                  "json": { "payload": { "type": "string" } }
                }
              }
            }
            """;

        // Act
        var group = AsyncApiRegistryReader.ReadGroup("orders", json);

        // Assert
        group.FindSchema("avro")!.Format.ShouldBe("Avro/1.9.0");
        group.FindSchema("json")!.Format.ShouldBe("JsonSchema/draft-07");
    }

    [Fact]
    public void Standalone_CopiesReferencedSchemasUnderDefinitions()
    {
        // Arrange
        var components = JsonNode.Parse("""
            {
              "order": { "type": "object", "properties": { "line": { "$ref": "#/components/schemas/line" }, "parent": { "$ref": "#/components/schemas/order" } } },
              "line": { "type": "object", "properties": { "sku": { "type": "string" } } },
              "unused": { "type": "string" }
            }
            """)!.AsObject();

        // Act
        var document = AsyncApiRegistryReader.Standalone(JsonNode.Parse("""{ "$ref": "#/components/schemas/order" }""")!, components);

        // Assert
        document["$schema"]!.GetValue<string>().ShouldBe("http://json-schema.org/draft-07/schema#");
        document["type"]!.GetValue<string>().ShouldBe("object");
        document["properties"]!["line"]!["$ref"]!.GetValue<string>().ShouldBe("#/definitions/line");
        document["properties"]!["parent"]!["$ref"]!.GetValue<string>().ShouldBe("#/definitions/order");
        document["definitions"]!.AsObject().Select(d => d.Key).ShouldBe(["line", "order"], ignoreOrder: true);
        // The source schemas are left untouched.
        components["order"]!["properties"]!["line"]!["$ref"]!.GetValue<string>().ShouldBe("#/components/schemas/line");
    }

    [Fact]
    public void Standalone_InlineSchemaWithoutReferences_HasNoDefinitions()
    {
        // Act
        var document = AsyncApiRegistryReader.Standalone(JsonNode.Parse("""{ "type": "string" }""")!, null);

        // Assert
        document.AsObject().ContainsKey("definitions").ShouldBeFalse();
        document["type"]!.GetValue<string>().ShouldBe("string");
    }

    [Theory]
    [InlineData("orders", "orders")]
    [InlineData("orders.order-placed", "orders.order-placed")]
    [InlineData("a b/c", "a_b_c")]
    [InlineData("-leading", "_-leading")]
    public void UniqueId_MakesAValidXRegistryId(string value, string expected)
    {
        // Act & Assert
        AsyncApiRegistryReader.UniqueId(value, []).ShouldBe(expected);
    }

    [Fact]
    public void UniqueId_NumbersAnIdAlreadyTaken()
    {
        // Arrange
        var taken = new HashSet<string>();

        // Act
        var first = AsyncApiRegistryReader.UniqueId("a b", taken);
        var second = AsyncApiRegistryReader.UniqueId("a/b", taken);

        // Assert
        first.ShouldBe("a_b");
        second.ShouldBe("a_b_2");
    }

    [Theory]
    [InlineData("plain", "plain")]
    [InlineData("100%", "100%25")]
    [InlineData("say \"hi\"", "say %22hi%22")]
    [InlineData("Euro €", "Euro %E2%82%AC")]
    public void EncodeHeaderValue_PercentEncodesWhatHttpHeadersCannotCarry(string value, string expected)
    {
        // Act & Assert
        XRegistryRequestHandler.EncodeHeaderValue(value).ShouldBe(expected);
    }

    [Fact]
    public void InlineSelection_FollowsTheSpecificationsPathRules()
    {
        // Arrange
        string[] valid = ["model", "messagegroups", "messagegroups.messages", "messagegroups.messages.versions", "schemagroups"];

        // Act
        var star = InlineSelection.Parse(new StringValues(""), valid, out _)!;
        var nested = InlineSelection.Parse(new StringValues("messagegroups.messages.versions"), valid, out _)!;
        var below = InlineSelection.Parse(new StringValues("messagegroups.*"), valid, out _)!;
        var invalid = InlineSelection.Parse(new StringValues("messagegroups,bogus"), valid, out var offending);

        // Assert
        star.Includes("messagegroups.messages").ShouldBeTrue();
        star.Includes("model").ShouldBeFalse();
        nested.Includes("messagegroups").ShouldBeTrue();
        nested.Includes("messagegroups.messages").ShouldBeTrue();
        nested.Includes("schemagroups").ShouldBeFalse();
        below.Includes("messagegroups").ShouldBeTrue();
        below.Includes("messagegroups.messages.versions").ShouldBeTrue();
        below.Includes("schemagroups").ShouldBeFalse();
        invalid.ShouldBeNull();
        offending.ShouldBe("bogus");
        InlineSelection.None.Includes("messagegroups").ShouldBeFalse();
    }

    [Fact]
    public void XRegistryOptions_ValidateArguments()
    {
        // Act & Assert
        Should.Throw<ArgumentException>(() => new XRegistryOptions().WithRegistry(" "));
        Should.Throw<ArgumentNullException>(() => new XRegistryOptions().IncludeDocuments(null!));
        Should.Throw<ArgumentException>(() => new XRegistryOptions().IncludeDocuments(""));
        Should.Throw<ArgumentNullException>(() => XRegistryServiceCollectionExtensions.AddXRegistry(null!));
    }

    [Fact]
    public void XRegistryOptions_IncludeDocuments_IsCaseInsensitive()
    {
        // Arrange
        var options = new XRegistryOptions().IncludeDocuments("V1");

        // Act & Assert
        options.IsIncluded("v1").ShouldBeTrue();
        options.IsIncluded("v2").ShouldBeFalse();
        new XRegistryOptions().IsIncluded("anything").ShouldBeTrue();
    }
}
