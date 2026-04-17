namespace LiveChatSignalR.Models;

/// <summary>
/// A chat message broadcast to all users in a room.
/// </summary>
public class ChatMessage
{
    /// <summary>
    /// The room the message was sent to.
    /// </summary>
    public string RoomId { get; set; } = string.Empty;

    /// <summary>
    /// The username of the sender.
    /// </summary>
    public string SenderUsername { get; set; } = string.Empty;

    /// <summary>
    /// The text content of the message.
    /// </summary>
    public string Content { get; set; } = string.Empty;

    /// <summary>
    /// UTC timestamp when the message was sent.
    /// </summary>
    public DateTime SentAt { get; set; }
}
