namespace LiveChatSignalR.Models;

/// <summary>
/// A private (direct) message sent from one user to another.
/// </summary>
public class PrivateMessage
{
    /// <summary>
    /// The username of the sender.
    /// </summary>
    public string SenderUsername { get; set; } = string.Empty;

    /// <summary>
    /// The username of the intended recipient.
    /// </summary>
    public string RecipientUsername { get; set; } = string.Empty;

    /// <summary>
    /// The text content of the private message.
    /// </summary>
    public string Content { get; set; } = string.Empty;

    /// <summary>
    /// UTC timestamp when the message was sent.
    /// </summary>
    public DateTime SentAt { get; set; }
}
