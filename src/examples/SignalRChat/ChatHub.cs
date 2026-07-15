using System.Collections.Concurrent;
using System.Runtime.CompilerServices;
using Bielu.AspNetCore.AsyncApi.Attributes.Attributes;
using Microsoft.AspNetCore.SignalR;

namespace SignalRChat;

/// <summary>A chat message exchanged over the hub.</summary>
public record ChatMessage(string User, string Text, DateTimeOffset SentAt)
{
    /// <summary>The room the message belongs to. <c>null</c> means it was broadcast to everyone.</summary>
    public string? Room { get; init; }
}

/// <summary>Raised to clients when a user connects to or disconnects from the hub.</summary>
public record PresenceEvent(string User, string ConnectionId, int OnlineCount, DateTimeOffset At);

/// <summary>Raised to clients while another user is composing a message.</summary>
public record TypingEvent(string User, bool IsTyping, string? Room);

/// <summary>
/// Strongly-typed contract for server-to-client pushes. Annotated with AsyncAPI attributes so the
/// document generator surfaces every push as a <c>subscribe</c> operation on the <c>chatHub</c>
/// channel (the messages a client receives), mirroring the client-to-server hub methods declared on
/// <see cref="ChatHub"/>.
/// </summary>
[AsyncApi("signalr")]
[Channel("chatHub", Description = "Real-time chat hub backed by ASP.NET Core SignalR.")]
public interface IChatClient
{
    /// <summary>Pushes a chat message to connected clients.</summary>
    [SubscribeOperation(typeof(ChatMessage), "chat", Summary = "A chat message was broadcast to the client.", BindingsRef = "receiveMessage")]
    Task ReceiveMessage(ChatMessage message);

    /// <summary>Notifies clients that a user joined the hub.</summary>
    [SubscribeOperation(typeof(PresenceEvent), "presence", Summary = "A user connected to the hub.", BindingsRef = "userJoined")]
    Task UserJoined(PresenceEvent presence);

    /// <summary>Notifies clients that a user left the hub.</summary>
    [SubscribeOperation(typeof(PresenceEvent), "presence", Summary = "A user disconnected from the hub.", BindingsRef = "userLeft")]
    Task UserLeft(PresenceEvent presence);

    /// <summary>Relays another user's typing indicator.</summary>
    [SubscribeOperation(typeof(TypingEvent), "presence", Summary = "Another user started or stopped typing.", BindingsRef = "typingChanged")]
    Task TypingChanged(TypingEvent typing);
}

/// <summary>
/// A real ASP.NET Core SignalR hub modelling a small chat application: presence tracking, broadcast
/// and per-room messaging, typing indicators and a streamed message backlog.
/// </summary>
/// <remarks>
/// It is also annotated with AsyncAPI attributes so the document generator surfaces it as the
/// <c>chatHub</c> channel. Each client-to-server hub method is a <c>publish</c> operation; the SignalR
/// protocol bindings are linked to the channel/operations via the <c>BindingsRef</c> values registered
/// in <c>Program.cs</c>.
/// </remarks>
[AsyncApi("signalr")]
[Channel("chatHub", BindingsRef = "chatHub", Description = "Real-time chat hub backed by ASP.NET Core SignalR.")]
public class ChatHub : Hub<IChatClient>
{
    // Connection registry (connectionId -> user) so presence can be reported as people come and go.
    private static readonly ConcurrentDictionary<string, string> Connections = new();

    // A small in-memory ring buffer of recent *broadcast* messages, streamed back to new clients on
    // request. Room-scoped messages are intentionally kept out so the backlog can be replayed to any
    // client without leaking another room's traffic.
    private const int BacklogSize = 50;
    private static readonly ConcurrentQueue<ChatMessage> Backlog = new();

    // Per-connection room membership so room-scoped sends can be authorised. SignalR's Groups API is
    // write-only (it cannot be queried), so the hub tracks which rooms each connection has joined.
    private static readonly ConcurrentDictionary<string, HashSet<string>> RoomMembership = new();

    /// <summary>The display name supplied on the connection query string, e.g. <c>?user=ada</c>.</summary>
    private string UserName =>
        Context.GetHttpContext()?.Request.Query["user"].ToString() is { Length: > 0 } name
            ? name
            : $"anon-{Context.ConnectionId[..6]}";

    /// <inheritdoc />
    public override async Task OnConnectedAsync()
    {
        Connections[Context.ConnectionId] = UserName;
        await Clients.Others.UserJoined(new PresenceEvent(UserName, Context.ConnectionId, Connections.Count, DateTimeOffset.UtcNow));
        await base.OnConnectedAsync();
    }

    /// <inheritdoc />
    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        if (Connections.TryRemove(Context.ConnectionId, out var user))
        {
            await Clients.Others.UserLeft(new PresenceEvent(user, Context.ConnectionId, Connections.Count, DateTimeOffset.UtcNow));
        }

        RoomMembership.TryRemove(Context.ConnectionId, out _);

        await base.OnDisconnectedAsync(exception);
    }

    /// <summary>Client-to-server invocation: broadcast a message to everyone connected to the hub.</summary>
    [PublishOperation(typeof(ChatMessage), "chat", Summary = "Broadcast a chat message to every connected client.", BindingsRef = "sendMessage")]
    public async Task SendMessage(ChatMessage message)
    {
        // Normalise once so the backlog and the live broadcast carry the identical (room-less) payload.
        var broadcast = message with { Room = null };
        Remember(broadcast);
        await Clients.All.ReceiveMessage(broadcast);
    }

    /// <summary>
    /// Client-to-server: send a message to a single chat room (group). The target room is carried on
    /// the message payload so the whole call is a single argument — which keeps the generated example
    /// directly invocable from the Scalar console.
    /// </summary>
    [PublishOperation(typeof(ChatMessage), "chat", Summary = "Send a chat message to a single room.", BindingsRef = "sendToRoom")]
    [Channel("chatHub")]
    public async Task SendToRoom(ChatMessage message)
    {
        var room = NormalizeRoom(message.Room);
        if (!IsInRoom(room))
        {
            throw new HubException("You must join the room before sending to it.");
        }

        // Deliberately NOT added to the backlog: room messages are excluded from the replayable
        // history so StreamHistory can never leak a room's messages to clients outside that room.
        // Forward the normalised room so the group key matches Join/Leave and clients see the canonical name.
        await Clients.Group(room).ReceiveMessage(message with { Room = room });
    }

    /// <summary>Adds the caller to a named chat room.</summary>
    [PublishOperation(typeof(string), Summary = "Join a named chat room.", BindingsRef = "joinRoom")]
    [Channel("chatHub")]
    public async Task JoinRoom(string room)
    {
        var normalized = NormalizeRoom(room);

        // Join the actual SignalR group first; only record membership once that succeeds, so
        // IsInRoom/SendToRoom never report a room the connection was not actually added to.
        await Groups.AddToGroupAsync(Context.ConnectionId, normalized);

        var rooms = RoomMembership.GetOrAdd(Context.ConnectionId, static _ => new HashSet<string>(StringComparer.Ordinal));
        lock (rooms)
        {
            rooms.Add(normalized);
        }
    }

    /// <summary>Removes the caller from a named chat room.</summary>
    [PublishOperation(typeof(string), Summary = "Leave a named chat room.", BindingsRef = "leaveRoom")]
    [Channel("chatHub")]
    public Task LeaveRoom(string room)
    {
        var normalized = NormalizeRoom(room);
        if (RoomMembership.TryGetValue(Context.ConnectionId, out var rooms))
        {
            lock (rooms)
            {
                rooms.Remove(normalized);
            }
        }

        return Groups.RemoveFromGroupAsync(Context.ConnectionId, normalized);
    }

    /// <summary>
    /// Fire-and-forget: tell other clients whether the caller is currently typing. Takes a single
    /// <see cref="TypingEvent"/> payload; the server stamps it with the caller's real name before
    /// relaying it so a client cannot spoof another user.
    /// </summary>
    [PublishOperation(typeof(TypingEvent), "presence", Summary = "Broadcast a typing indicator to other clients.", BindingsRef = "notifyTyping")]
    [Channel("chatHub")]
    public Task NotifyTyping(TypingEvent typing)
        => Clients.Others.TypingChanged(typing with { User = UserName });

    /// <summary>
    /// Request/response hub method: returns the users currently connected to the hub. Because the
    /// caller awaits a result this is a SignalR <c>invocation</c> (<c>connection.invoke</c>), in
    /// contrast to the fire-and-forget <c>send</c> methods above.
    /// </summary>
    [PublishOperation(Summary = "Return the users currently connected to the hub.", BindingsRef = "getOnlineUsers")]
    [Channel("chatHub")]
    public Task<IReadOnlyCollection<string>> GetOnlineUsers()
        => Task.FromResult<IReadOnlyCollection<string>>(Connections.Values.Distinct().ToArray());

    /// <summary>
    /// Streaming hub method: replays the most recent <paramref name="count"/> broadcast messages to
    /// the caller as a SignalR stream. Only room-less broadcasts are kept in the backlog, so this can
    /// never leak room-scoped traffic. Showcases the <c>streamInvocation</c> call type; the single
    /// <c>int</c> argument is what the caller supplies, so the generated console example is invocable.
    /// </summary>
    [PublishOperation(typeof(int), "chat", Summary = "Stream the recent message backlog to the caller.", BindingsRef = "streamHistory")]
    [Channel("chatHub")]
    public async IAsyncEnumerable<ChatMessage> StreamHistory(
        int count,
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        foreach (var message in Backlog.Reverse().Take(count).Reverse())
        {
            cancellationToken.ThrowIfCancellationRequested();
            yield return message;
            await Task.Delay(50, cancellationToken);
        }
    }

    /// <summary>Trim a room name and reject it if null, empty or whitespace-only.</summary>
    private static string NormalizeRoom(string? room)
    {
        var trimmed = room?.Trim();
        if (string.IsNullOrEmpty(trimmed))
        {
            throw new HubException("A non-empty room name is required.");
        }

        return trimmed;
    }

    /// <summary>Whether the calling connection has joined <paramref name="room"/>.</summary>
    private bool IsInRoom(string room)
    {
        if (!RoomMembership.TryGetValue(Context.ConnectionId, out var rooms))
        {
            return false;
        }

        lock (rooms)
        {
            return rooms.Contains(room);
        }
    }

    private static void Remember(ChatMessage message)
    {
        Backlog.Enqueue(message);
        while (Backlog.Count > BacklogSize && Backlog.TryDequeue(out _))
        {
            // Trim the backlog to its most recent entries.
        }
    }
}
