namespace LiveChatSignalR.Models;

/// <summary>
/// Event raised when a user joins or leaves a chat.
/// </summary>
public class UserPresenceEvent
{
    /// <summary>
    /// The chat the event relates to.
    /// </summary>
    public string ChatId { get; set; } = string.Empty;

    /// <summary>
    /// The username of the user who joined or left.
    /// </summary>
    public string Username { get; set; } = string.Empty;

    /// <summary>
    /// Presence action: "Joined" or "Left".
    /// </summary>
    public string Action { get; set; } = string.Empty;

    /// <summary>
    /// UTC timestamp of the presence event.
    /// </summary>
    public DateTime OccurredAt { get; set; }
}
