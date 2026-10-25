using System.Text.Json.Nodes;
using Shouldly;
using Xunit;

namespace Bielu.AspNetCore.ApiDescriptions.Mcp.Tests.Unit;

public class AsyncApiOperationSearchTests
{
    private static readonly JsonObject Document = JsonNode.Parse("""
        {
          "asyncapi": "3.0.0",
          "servers": { "hub": { "host": "localhost", "protocol": "signalr" } },
          "channels": {
            "chat": { "address": "chatHub", "messages": { "chatMessage": { "title": "A chat message" } }, "bindings": { "ws": {} } }
          },
          "operations": {
            "ChatHub_SendMessage_Receive": {
              "action": "receive",
              "channel": { "$ref": "#/channels/chat" },
              "summary": "Broadcast to everyone.",
              "tags": [ { "name": "chat" } ],
              "bindings": { "signalr": { "target": "SendMessage" } },
              "messages": [ { "$ref": "#/channels/chat/messages/chatMessage" } ]
            },
            "ChatHub_JoinRoom_Receive": {
              "action": "receive",
              "channel": { "$ref": "#/channels/chat" },
              "description": "Join a room to get its messages."
            }
          }
        }
        """)!.AsObject();

    [Theory]
    [InlineData("send chat messages", new[] { "send", "chat", "message" })]
    [InlineData("SendChatMessage", new[] { "send", "chat", "message" })]
    [InlineData("HTTPServer v2Orders", new[] { "http", "server", "v", "2", "order" })]
    [InlineData("status address bus", new[] { "status", "address", "bus" })]
    [InlineData("  ", new string[0])]
    [InlineData(null, new string[0])]
    public void Terms_SplitsIdentifiersAndDropsPluralS(string? query, string[] expected)
    {
        // Act & Assert
        AsyncApiOperationSearch.Terms(query).ShouldBe(expected);
    }

    [Fact]
    public void Match_RanksIdAndBindingMatchesAboveDescriptions()
    {
        // Arrange
        var terms = AsyncApiOperationSearch.Terms("send message");
        var send = AsyncApiDocumentInspector.FindOperation(Document, "ChatHub_SendMessage_Receive")!;
        var join = AsyncApiDocumentInspector.FindOperation(Document, "ChatHub_JoinRoom_Receive")!;

        // Act
        var sendMatch = AsyncApiOperationSearch.Match(Document, send, terms);
        var joinMatch = AsyncApiOperationSearch.Match(Document, join, terms);

        // Assert
        sendMatch.ShouldBe((2, 10));
        joinMatch.ShouldBe((1, 1));
    }

    [Fact]
    public void Match_FindsBindingTargetsTagsAndMessageTitles()
    {
        // Arrange
        var send = AsyncApiDocumentInspector.FindOperation(Document, "ChatHub_SendMessage_Receive")!;

        // Act & Assert
        AsyncApiOperationSearch.Match(Document, send, ["broadcast"]).Matched.ShouldBe(1);
        AsyncApiOperationSearch.Match(Document, send, ["hub"]).Matched.ShouldBe(1);
        AsyncApiOperationSearch.Match(Document, send, ["nothing"]).Matched.ShouldBe(0);
        AsyncApiOperationSearch.Match(Document, send, []).ShouldBe((0, 0));
    }

    [Fact]
    public void Protocols_CombinesServerProtocolsAndBindingKeys()
    {
        // Arrange
        var send = AsyncApiDocumentInspector.FindOperation(Document, "ChatHub_SendMessage_Receive")!;

        // Act & Assert
        AsyncApiOperationSearch.Protocols(Document, send).ShouldBe(["signalr", "ws"]);
    }
}
