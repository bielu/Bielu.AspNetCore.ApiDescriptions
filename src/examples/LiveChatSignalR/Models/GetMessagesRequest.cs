using System.ComponentModel.DataAnnotations;

namespace LiveChatSignalR.Models;

/// <summary>
/// Request model for retrieving historical chat messages with pagination and an optional date offset.
/// </summary>
public class GetMessagesRequest
{
    /// <summary>
    /// The chat to retrieve messages from.
    /// </summary>
    [Required]
    [StringLength(100, MinimumLength = 1)]
    public string ChatId { get; set; } = string.Empty;

    /// <summary>
    /// Page number (1-based). Defaults to 1.
    /// </summary>
    [Range(1, int.MaxValue)]
    public int Page { get; set; } = 1;

    /// <summary>
    /// Number of messages per page. Defaults to 50, maximum 100.
    /// </summary>
    [Range(1, 100)]
    public int PageSize { get; set; } = 50;

    /// <summary>
    /// Optional UTC date-time offset. When provided, only messages sent before
    /// this timestamp are returned. Useful for loading older messages on scroll.
    /// </summary>
    public DateTime? Before { get; set; }
}
