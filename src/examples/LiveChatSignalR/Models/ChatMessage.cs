namespace LiveChatSignalR.Models;

/// <summary>
/// A chat message delivered to all members of a chat.
/// Used for both group rooms and private (1-on-1) conversations.
/// </summary>
public class ChatMessage
{
    /// <summary>
    /// The chat this message belongs to (room or private conversation).
    /// </summary>
    public string ChatId { get; set; } = string.Empty;

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
