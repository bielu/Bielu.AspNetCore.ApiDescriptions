using System.Text.Json.Nodes;
using Microsoft.Extensions.DependencyInjection;
using Shouldly;
using Xunit;

namespace Bielu.AspNetCore.ApiDescriptions.Mcp.Tests.Unit;

public class AsyncApiDocumentInspectorTests
{
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
                  "publish": { "message": { "oneOf": [ { "$ref": "#/components/messages/a" }, { "$ref": "#/components/messages/b" } ] } }
                }
              }
            }
            """)!.AsObject();

        // Act
        var operations = AsyncApiDocumentInspector.ListOperations(document);

        // Assert
        operations.Count.ShouldBe(2);
        operations[0]!["id"]!.GetValue<string>().ShouldBe("sendOrder");
        operations[0]!["action"]!.GetValue<string>().ShouldBe("send");
        operations[0]!["address"]!.GetValue<string>().ShouldBe("orders");
        operations[0]!["messages"]!.AsArray().Select(m => m!.GetValue<string>()).ShouldBe(["order"]);
        operations[1]!["id"]!.GetValue<string>().ShouldBe("publish orders");
        operations[1]!["action"]!.GetValue<string>().ShouldBe("receive");
        operations[1]!["messages"]!.AsArray().Select(m => m!.GetValue<string>()).ShouldBe(["a", "b"]);
    }

    [Fact]
    public void GetMessage_ResolvesSchemasAndOtherComponentsTransitively()
    {
        // Arrange
        var document = JsonNode.Parse("""
            {
              "asyncapi": "3.1.0",
              "components": {
                "messages": {
                  "order": {
                    "payload": { "$ref": "#/components/schemas/order" },
                    "traits": [ { "$ref": "#/components/messageTraits/common" } ]
                  }
                },
                "schemas": {
                  "order": { "type": "object", "properties": { "line": { "$ref": "#/components/schemas/line" }, "self": { "$ref": "#/components/schemas/order" } } },
                  "line": { "type": "string" }
                },
                "messageTraits": { "common": { "contentType": "application/json" } }
              }
            }
            """)!.AsObject();

        // Act
        var message = AsyncApiDocumentInspector.GetMessage(document, "order")!;

        // Assert
        message["payload"]!["$ref"]!.GetValue<string>().ShouldBe("#/definitions/order");
        message["traits"]![0]!["$ref"]!.GetValue<string>().ShouldBe("#/definitions/messageTraits.common");
        message["definitions"]!.AsObject().Select(d => d.Key).ShouldBe(["order", "line", "messageTraits.common"], ignoreOrder: true);
        message["definitions"]!["order"]!["properties"]!["self"]!["$ref"]!.GetValue<string>().ShouldBe("#/definitions/order");
        // The document itself is left as it was.
        document["components"]!["schemas"]!["order"]!["properties"]!["line"]!["$ref"]!.GetValue<string>()
            .ShouldBe("#/components/schemas/line");
    }

    [Fact]
    public void GetMessage_Missing_ReturnsNull()
    {
        // Act & Assert
        AsyncApiDocumentInspector.GetMessage(new JsonObject(), "order").ShouldBeNull();
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
}
