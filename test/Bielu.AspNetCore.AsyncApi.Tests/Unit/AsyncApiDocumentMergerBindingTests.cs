using Bielu.AspNetCore.AsyncApi.Merger.Merge;
using ByteBard.AsyncAPI.Bindings.Kafka;
using Shouldly;
using Xunit;

namespace Bielu.AspNetCore.AsyncApi.Tests.Unit;

/// <summary>
/// The merger re-reads each service's document from JSON, so it must keep protocol bindings: a gateway that
/// dropped them would serve a merged document missing every server, channel, operation and message binding.
/// </summary>
public class AsyncApiDocumentMergerBindingTests
{
    private const string DocumentWithKafkaBindings = """
        {
          "asyncapi": "3.0.0",
          "info": { "title": "Orders", "version": "1.0.0" },
          "servers": {
            "broker": {
              "host": "kafka.example.com:9092",
              "protocol": "kafka",
              "bindings": { "kafka": { "schemaRegistryUrl": "https://registry.example.com", "bindingVersion": "0.5.0" } }
            }
          },
          "channels": {
            "orders": {
              "address": "orders",
              "bindings": { "kafka": { "topic": "orders", "partitions": 3, "bindingVersion": "0.5.0" } },
              "messages": {
                "orderPlaced": {
                  "payload": { "type": "object" },
                  "bindings": { "kafka": { "key": { "type": "string" }, "bindingVersion": "0.5.0" } }
                }
              }
            }
          },
          "operations": {
            "publishOrder": {
              "action": "send",
              "channel": { "$ref": "#/channels/orders" },
              "bindings": { "kafka": { "groupId": { "type": "string" }, "bindingVersion": "0.5.0" } }
            }
          }
        }
        """;

    [Fact]
    public void ParseDocument_KafkaBindings_AreKept()
    {
        // Act
        var document = AsyncApiDocumentMerger.ParseDocument(DocumentWithKafkaBindings, "https://orders/asyncapi.json");

        // Assert
        document.Servers["broker"].Bindings.Values.OfType<KafkaServerBinding>().ShouldHaveSingleItem()
            .SchemaRegistryUrl.ShouldBe("https://registry.example.com");
        document.Channels["orders"].Bindings.Values.OfType<KafkaChannelBinding>().ShouldHaveSingleItem()
            .Partitions.ShouldBe(3);
        document.Operations["publishOrder"].Bindings.Values.OfType<KafkaOperationBinding>().ShouldHaveSingleItem();
        document.Channels["orders"].Messages["orderPlaced"].Bindings.Values.OfType<KafkaMessageBinding>().ShouldHaveSingleItem();
    }

    [Fact]
    public void MergeDocuments_ParsedDocumentsWithBindings_KeepThemInTheMergedDocument()
    {
        // Arrange
        var orders = AsyncApiDocumentMerger.ParseDocument(DocumentWithKafkaBindings, "https://orders/asyncapi.json");

        // Act
        var merged = AsyncApiDocumentMerger.MergeDocuments([(orders, null)]);

        // Assert
        merged.Channels["orders"].Bindings.Values.OfType<KafkaChannelBinding>().ShouldHaveSingleItem().Topic.ShouldBe("orders");
        merged.Servers["broker"].Bindings.Values.OfType<KafkaServerBinding>().ShouldHaveSingleItem();
    }
}
