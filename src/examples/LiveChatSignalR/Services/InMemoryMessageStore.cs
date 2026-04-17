using System.Collections.Concurrent;
using LiveChatSignalR.Models;

namespace LiveChatSignalR.Services;

/// <summary>
/// Simple thread-safe in-memory store for chat messages.
/// In a production application this would be backed by a database.
/// </summary>
public class InMemoryMessageStore
{
    private readonly ConcurrentDictionary<string, List<ChatMessage>> _messages = new();
    private readonly object _lock = new();

    /// <summary>
    /// Stores a message in the in-memory store.
    /// </summary>
    public void AddMessage(ChatMessage message)
    {
        lock (_lock)
        {
            var list = _messages.GetOrAdd(message.ChatId, _ => []);
            list.Add(message);
        }
    }

    /// <summary>
    /// Retrieves a page of messages for a given chat, optionally filtered
    /// to only messages sent before a specified date-time offset.
    /// Messages are returned newest-first.
    /// </summary>
    public (List<ChatMessage> Messages, int TotalCount) GetMessages(
        string chatId, int page, int pageSize, DateTime? before)
    {
        lock (_lock)
        {
            if (!_messages.TryGetValue(chatId, out var all))
            {
                return ([], 0);
            }

            IEnumerable<ChatMessage> filtered = all;

            if (before.HasValue)
            {
                filtered = filtered.Where(m => m.SentAt < before.Value);
            }

            var ordered = filtered.OrderByDescending(m => m.SentAt).ToList();
            var totalCount = ordered.Count;
            var paged = ordered.Skip((page - 1) * pageSize).Take(pageSize).ToList();

            return (paged, totalCount);
        }
    }
}
