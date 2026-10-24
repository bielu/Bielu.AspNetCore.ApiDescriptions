using System.Text.Json.Nodes;
using ByteBard.AsyncAPI.Bindings;
using ByteBard.AsyncAPI.Models;
using ByteBard.AsyncAPI.Readers;
using ByteBard.AsyncAPI.Writers;
using Shouldly;
using Xunit;

namespace Bielu.AspNetCore.AsyncApi.Extensions.Protocols.SignalR.Tests;

/// <summary>
/// Reads every SignalR binding (server, channel, operation, message) from a document with all fields set, then
/// writes each back out: the bindings must parse into their typed form and serialize to exactly what was read.
/// </summary>
public class SignalRBindingReadRoundTripTests
{
    private const string Document = """
        {
          "asyncapi": "3.0.0",
          "info": { "title": "SignalR", "version": "1.0.0" },
          "servers": {
            "hub": {
              "host": "chat.example.com",
              "protocol": "signalr",
              "bindings": { "signalr": { "transports": ["webSockets", "longPolling"], "protocols": ["json", "messagepack"], "bindingVersion": "0.1.0" } }
            }
          },
          "channels": {
            "chat": {
              "address": "/hubs/chat",
              "bindings": {
                "signalr": {
                  "hub": "ChatHub",
                  "transports": ["webSockets"],
                  "protocols": ["json"],
                  "query": { "type": "object" },
                  "headers": { "type": "object" },
                  "bindingVersion": "0.1.0"
                }
              },
              "messages": {
                "chatMessage": {
                  "payload": { "type": "string" },
                  "bindings": {
                    "signalr": { "hubProtocol": "json", "messageType": "streamItem", "headers": { "type": "object" }, "bindingVersion": "0.1.0" }
                  }
                }
              }
            }
          },
          "operations": {
            "sendMessage": {
              "action": "send",
              "channel": { "$ref": "#/channels/chat" },
              "bindings": {
                "signalr": { "target": "SendMessage", "direction": "clientToServer", "callType": "invoke", "streaming": true, "bindingVersion": "0.1.0" }
              }
            }
          }
        }
        """;

    private static AsyncApiDocument Read()
    {
        var settings = new AsyncApiReaderSettings();
        settings.Bindings.Add(new SignalRServerBinding());
        settings.Bindings.Add(new SignalRChannelBinding());
        settings.Bindings.Add(new SignalROperationBinding());
        settings.Bindings.Add(new SignalRMessageBinding());

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

        JsonNode.DeepEquals(Write(binding, v2: false), expected).ShouldBeTrue($"V3 {pointer}: {Write(binding, v2: false).ToJsonString()}");
        JsonNode.DeepEquals(Write(binding, v2: true), expected).ShouldBeTrue($"V2 {pointer}: {Write(binding, v2: true).ToJsonString()}");
    }

    [Fact]
    public void Read_AllBindings_ParseEveryField()
    {
        // Act
        var document = Read();

        // Assert
        var server = document.Servers["hub"].Bindings.Values.OfType<SignalRServerBinding>().ShouldHaveSingleItem();
        server.Transports.ShouldBe(["webSockets", "longPolling"]);
        server.Protocols.ShouldBe(["json", "messagepack"]);

        var channel = document.Channels["chat"].Bindings.Values.OfType<SignalRChannelBinding>().ShouldHaveSingleItem();
        channel.Hub.ShouldBe("ChatHub");
        channel.Query.ShouldNotBeNull();
        channel.Headers.ShouldNotBeNull();

        var operation = document.Operations["sendMessage"].Bindings.Values.OfType<SignalROperationBinding>().ShouldHaveSingleItem();
        operation.Target.ShouldBe("SendMessage");
        operation.Streaming.ShouldBe(true);

        var message = document.Channels["chat"].Messages["chatMessage"].Bindings.Values.OfType<SignalRMessageBinding>().ShouldHaveSingleItem();
        message.HubProtocol.ShouldBe("json");
        message.MessageType.ShouldBe(SignalRMessageType.StreamItem);
    }

    [Fact]
    public void Write_ReadBindings_ReproduceTheSourceJson()
    {
        // Arrange
        var document = Read();

        // Act & Assert
        ShouldRoundTrip(document.Servers["hub"].Bindings.Values.OfType<AsyncApiBinding>().Single(), "/servers/hub/bindings/signalr");
        ShouldRoundTrip(document.Channels["chat"].Bindings.Values.OfType<AsyncApiBinding>().Single(), "/channels/chat/bindings/signalr");
        ShouldRoundTrip(document.Operations["sendMessage"].Bindings.Values.OfType<AsyncApiBinding>().Single(), "/operations/sendMessage/bindings/signalr");
        ShouldRoundTrip(document.Channels["chat"].Messages["chatMessage"].Bindings.Values.OfType<AsyncApiBinding>().Single(), "/channels/chat/messages/chatMessage/bindings/signalr");
    }
}
