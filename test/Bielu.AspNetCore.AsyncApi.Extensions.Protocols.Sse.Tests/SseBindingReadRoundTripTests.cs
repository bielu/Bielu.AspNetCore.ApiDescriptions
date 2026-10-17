using System.Text.Json.Nodes;
using ByteBard.AsyncAPI.Bindings;
using ByteBard.AsyncAPI.Models;
using ByteBard.AsyncAPI.Readers;
using ByteBard.AsyncAPI.Writers;
using Shouldly;
using Xunit;

namespace Bielu.AspNetCore.AsyncApi.Extensions.Protocols.Sse.Tests;

/// <summary>
/// Reads every SSE binding (server, channel, operation, message) from a document with all fields set, then writes
/// each back out: the bindings must parse into their typed form and serialize to exactly what was read.
/// </summary>
public class SseBindingReadRoundTripTests
{
    private const string Document = """
        {
          "asyncapi": "3.0.0",
          "info": { "title": "SSE", "version": "1.0.0" },
          "servers": {
            "events": {
              "host": "events.example.com",
              "protocol": "sse",
              "bindings": { "sse": { "retry": 5000, "heartbeat": true, "bindingVersion": "0.1.0" } }
            }
          },
          "channels": {
            "prices": {
              "address": "/prices",
              "bindings": {
                "sse": {
                  "path": "/prices",
                  "method": "GET",
                  "contentType": "text/event-stream",
                  "query": { "type": "object" },
                  "headers": { "type": "object" },
                  "bindingVersion": "0.1.0"
                }
              },
              "messages": {
                "priceUpdate": {
                  "payload": { "type": "string" },
                  "bindings": {
                    "sse": { "event": "price-update", "id": "42", "retry": 3000, "headers": { "type": "object" }, "bindingVersion": "0.1.0" }
                  }
                }
              }
            }
          },
          "operations": {
            "streamPrices": {
              "action": "send",
              "channel": { "$ref": "#/channels/prices" },
              "bindings": { "sse": { "method": "GET", "direction": "serverToClient", "bindingVersion": "0.1.0" } }
            }
          }
        }
        """;

    private static AsyncApiDocument Read()
    {
        var settings = new AsyncApiReaderSettings();
        settings.Bindings.Add(new SseServerBinding());
        settings.Bindings.Add(new SseChannelBinding());
        settings.Bindings.Add(new SseOperationBinding());
        settings.Bindings.Add(new SseMessageBinding());

        var document = new AsyncApiStringReader(settings).Read(Document, out var diagnostic);
        diagnostic.Errors.ShouldBeEmpty();
        return document;
    }

    private static JsonNode Write(AsyncApiBinding binding, bool v2)
    {
        using var stringWriter = new StringWriter();
        var writer = new AsyncApiJsonWriter(stringWriter);
        if (v2)
        {
            binding.SerializeV2(writer);
        }
        else
        {
            binding.SerializeV3(writer);
        }

        return JsonNode.Parse(stringWriter.ToString())!;
    }

    private static void ShouldRoundTrip(AsyncApiBinding binding, string pointer)
    {
        var expected = JsonNode.Parse(Document)!;
        foreach (var segment in pointer.Split('/', StringSplitOptions.RemoveEmptyEntries))
        {
            expected = expected[segment]!;
        }

        JsonNode.DeepEquals(Write(binding, v2: false), expected).ShouldBeTrue($"V3 {pointer}");
        JsonNode.DeepEquals(Write(binding, v2: true), expected).ShouldBeTrue($"V2 {pointer}");
    }

    [Fact]
    public void Read_AllBindings_ParseEveryField()
    {
        // Act
        var document = Read();

        // Assert
        var server = document.Servers["events"].Bindings.Values.OfType<SseServerBinding>().ShouldHaveSingleItem();
        server.Retry.ShouldBe(5000);
        server.Heartbeat.ShouldBe(true);

        var channel = document.Channels["prices"].Bindings.Values.OfType<SseChannelBinding>().ShouldHaveSingleItem();
        channel.Path.ShouldBe("/prices");
        channel.Method.ShouldBe(SseProtocol.Methods.Get);
        channel.ContentType.ShouldBe(SseProtocol.EventStreamContentType);
        channel.Query.ShouldNotBeNull();
        channel.Headers.ShouldNotBeNull();

        var operation = document.Operations["streamPrices"].Bindings.Values.OfType<SseOperationBinding>().ShouldHaveSingleItem();
        operation.Direction.ShouldBe(SseProtocol.Directions.ServerToClient);

        var message = document.Channels["prices"].Messages["priceUpdate"].Bindings.Values.OfType<SseMessageBinding>().ShouldHaveSingleItem();
        message.Event.ShouldBe("price-update");
        message.Id.ShouldBe("42");
        message.Retry.ShouldBe(3000);
        message.Headers.ShouldNotBeNull();
    }

    [Fact]
    public void Write_ReadBindings_ReproduceTheSourceJson()
    {
        // Arrange
        var document = Read();

        // Act & Assert
        ShouldRoundTrip(document.Servers["events"].Bindings.Values.OfType<AsyncApiBinding>().Single(), "/servers/events/bindings/sse");
        ShouldRoundTrip(document.Channels["prices"].Bindings.Values.OfType<AsyncApiBinding>().Single(), "/channels/prices/bindings/sse");
        ShouldRoundTrip(document.Operations["streamPrices"].Bindings.Values.OfType<AsyncApiBinding>().Single(), "/operations/streamPrices/bindings/sse");
        ShouldRoundTrip(document.Channels["prices"].Messages["priceUpdate"].Bindings.Values.OfType<AsyncApiBinding>().Single(), "/channels/prices/messages/priceUpdate/bindings/sse");
    }
}
