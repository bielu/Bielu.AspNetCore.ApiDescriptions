namespace LiveChatSignalR.Models;

/// <summary>
/// Response model containing a page of chat messages.
/// </summary>
public class GetMessagesResponse
{
    /// <summary>
    /// The chat these messages belong to.
    /// </summary>
    public string ChatId { get; set; } = string.Empty;

    /// <summary>
    /// The returned page of messages, ordered from newest to oldest.
    /// </summary>
    public List<ChatMessage> Messages { get; set; } = [];

    /// <summary>
    /// Total number of messages matching the query (before pagination).
    /// </summary>
    public int TotalCount { get; set; }

    /// <summary>
    /// Current page number (1-based).
    /// </summary>
    public int Page { get; set; }

    /// <summary>
    /// Page size used for this response.
    /// </summary>
    public int PageSize { get; set; }

    /// <summary>
    /// Whether there are more messages available after this page.
    /// </summary>
    public bool HasMore { get; set; }
}
