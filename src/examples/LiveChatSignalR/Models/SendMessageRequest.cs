using System.ComponentModel.DataAnnotations;

namespace LiveChatSignalR.Models;

/// <summary>
/// Request model sent by a client to post a message in a chat.
/// </summary>
public class SendMessageRequest
{
    /// <summary>
    /// The target chat identifier (room or private conversation).
    /// </summary>
    [Required]
    [StringLength(100, MinimumLength = 1)]
    public string ChatId { get; set; } = string.Empty;

    /// <summary>
    /// The text content of the message to send.
    /// </summary>
    [Required]
    [StringLength(2000, MinimumLength = 1)]
    public string Content { get; set; } = string.Empty;
}
