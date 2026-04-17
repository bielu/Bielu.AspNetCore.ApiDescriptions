namespace LiveChatSignalR.Models;

/// <summary>
/// Request model sent by a client to post a message in a chat.
/// </summary>
public class SendMessageRequest
{
    /// <summary>
    /// The target chat identifier (room or private conversation).
    /// </summary>
    public string ChatId { get; set; } = string.Empty;

    /// <summary>
    /// The text content of the message to send.
    /// </summary>
    public string Content { get; set; } = string.Empty;
}
