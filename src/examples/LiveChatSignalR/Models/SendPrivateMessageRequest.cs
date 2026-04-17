namespace LiveChatSignalR.Models;

/// <summary>
/// Request model sent by a client to deliver a private (direct) message.
/// </summary>
public class SendPrivateMessageRequest
{
    /// <summary>
    /// The username of the intended recipient.
    /// </summary>
    public string RecipientUsername { get; set; } = string.Empty;

    /// <summary>
    /// The text content of the private message.
    /// </summary>
    public string Content { get; set; } = string.Empty;
}
