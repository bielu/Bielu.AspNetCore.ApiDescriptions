namespace LiveChatSignalR.Models;

/// <summary>
/// Request model sent by a client to broadcast a message in a chat room.
/// </summary>
public class SendMessageRequest
{
    /// <summary>
    /// The target chat room identifier (e.g. "general", "support").
    /// </summary>
    public string RoomId { get; set; } = string.Empty;

    /// <summary>
    /// The text content of the message to send.
    /// </summary>
    public string Content { get; set; } = string.Empty;
}
