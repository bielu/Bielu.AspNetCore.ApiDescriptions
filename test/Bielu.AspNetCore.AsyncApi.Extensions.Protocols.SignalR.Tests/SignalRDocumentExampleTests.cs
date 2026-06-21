using System.Text.Json.Nodes;
using Bielu.AspNetCore.AsyncApi.Extensions.Protocols.SignalR;
using ByteBard.AsyncAPI.Models;
using ByteBard.AsyncAPI.Models.Interfaces;
using ByteBard.AsyncAPI.Writers;
using Shouldly;
using Xunit;

namespace Bielu.AspNetCore.AsyncApi.Extensions.Protocols.SignalR.Tests;

/// <summary>
/// End-to-end example: builds a complete AsyncAPI document describing a SignalR chat hub using all
/// four SignalR bindings (server, channel, operation, message) and serializes it to AsyncAPI v3 JSON.
/// </summary>
public class SignalRDocumentExampleTests
{
    private static AsyncApiDocument BuildChatHubDocument()
    {
        // 1. Server: a SignalR endpoint advertising its transports and hub protocols.
        var server = new AsyncApiServer
        {
            Host = "localhost:5001",
            Protocol = SignalRProtocol.ProtocolName,
            Bindings = new AsyncApiBindings<IServerBinding>
            {
                new SignalRServerBinding
                {
                    Transports =
                    {
                        SignalRProtocol.Transports.WebSockets,
                        SignalRProtocol.Transports.ServerSentEvents,
                    },
                    Protocols = { SignalRProtocol.HubProtocols.Json, SignalRProtocol.HubProtocols.MessagePack },
                },
            },
        };

        // 2. Message: a chat message frame carried over the JSON hub protocol.
        var chatMessage = new AsyncApiMessage
        {
            Name = "ChatMessage",
            Bindings = new AsyncApiBindings<IMessageBinding>
            {
                new SignalRMessageBinding
                {
                    HubProtocol = SignalRProtocol.HubProtocols.Json,
                    MessageType = SignalRMessageType.Invocation,
                },
            },
        };

        // 3. Channel: the chat hub itself.
        var channel = new AsyncApiChannel
        {
            Address = "/chatHub",
            Messages = { ["chatMessage"] = chatMessage },
            Bindings = new AsyncApiBindings<IChannelBinding>
            {
                new SignalRChannelBinding
                {
                    Hub = "/chatHub",
                    Transports = { SignalRProtocol.Transports.WebSockets },
                    Protocols = { SignalRProtocol.HubProtocols.Json },
                },
            },
        };

        // 4. Operation: the client-to-server "SendMessage" hub method invocation.
        var operation = new AsyncApiOperation
        {
            Action = AsyncApiAction.Send,
            Channel = new AsyncApiChannelReference("#/channels/chatHub"),
            Bindings = new AsyncApiBindings<IOperationBinding>
            {
                new SignalROperationBinding
                {
                    Target = "SendMessage",
                    Direction = SignalRProtocol.Directions.ClientToServer,
                    CallType = SignalRProtocol.CallTypes.Invocation,
                },
            },
        };

        var document = new AsyncApiDocument
        {
            Info = new AsyncApiInfo { Title = "Chat Hub", Version = "1.0.0" },
        };
        document.Servers["signalr"] = server;
        document.Channels["chatHub"] = channel;
        document.Operations["sendMessage"] = operation;
        return document;
    }

    [Fact]
    public void ChatHubDocument_SerializesWithSignalRBindings()
    {
        var document = BuildChatHubDocument();

        using var stringWriter = new StringWriter();
        document.SerializeV3(new AsyncApiJsonWriter(stringWriter));
        stringWriter.Flush();
        var json = JsonNode.Parse(stringWriter.ToString())!;

        json["servers"]!["signalr"]!["bindings"]!["signalr"]!["transports"]!
            .AsArray().Select(n => n!.GetValue<string>()).ShouldContain("webSockets");

        json["channels"]!["chatHub"]!["bindings"]!["signalr"]!["hub"]!
            .GetValue<string>().ShouldBe("/chatHub");

        json["operations"]!["sendMessage"]!["bindings"]!["signalr"]!["target"]!
            .GetValue<string>().ShouldBe("SendMessage");

        json["channels"]!["chatHub"]!["messages"]!["chatMessage"]!["bindings"]!["signalr"]!["hubProtocol"]!
            .GetValue<string>().ShouldBe("json");
    }
}
