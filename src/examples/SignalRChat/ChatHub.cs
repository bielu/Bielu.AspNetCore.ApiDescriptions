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
[AsyncApi]
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
[AsyncApi]
[Channel("chatHub", BindingsRef = "chatHub", Description = "Real-time chat hub backed by ASP.NET Core SignalR.")]
public class ChatHub : Hub<IChatClient>
{
    // Connection registry (connectionId -> user) so presence can be reported as people come and go.
    private static readonly ConcurrentDictionary<string, string> Connections = new();

    // A small in-memory ring buffer of recent messages, streamed back to new clients on request.
    private const int BacklogSize = 50;
    private static readonly ConcurrentQueue<ChatMessage> Backlog = new();

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

        await base.OnDisconnectedAsync(exception);
    }

    /// <summary>Client-to-server invocation: broadcast a message to everyone connected to the hub.</summary>
    [PublishOperation(typeof(ChatMessage), "chat", Summary = "Broadcast a chat message to every connected client.", BindingsRef = "sendMessage")]
    public async Task SendMessage(ChatMessage message)
    {
        Remember(message with { Room = null });
        await Clients.All.ReceiveMessage(message);
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
        Remember(message);
        await Clients.Group(message.Room ?? string.Empty).ReceiveMessage(message);
    }

    /// <summary>Adds the caller to a named chat room.</summary>
    [PublishOperation(typeof(string), Summary = "Join a named chat room.", BindingsRef = "joinRoom")]
    [Channel("chatHub")]
    public Task JoinRoom(string room)
        => Groups.AddToGroupAsync(Context.ConnectionId, room);

    /// <summary>Removes the caller from a named chat room.</summary>
    [PublishOperation(typeof(string), Summary = "Leave a named chat room.", BindingsRef = "leaveRoom")]
    [Channel("chatHub")]
    public Task LeaveRoom(string room)
        => Groups.RemoveFromGroupAsync(Context.ConnectionId, room);

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
    /// Streaming hub method: replays the most recent <paramref name="count"/> messages to the caller
    /// as a SignalR stream. Showcases the <c>streamInvocation</c> call type; the single <c>int</c>
    /// argument is what the caller supplies, so the generated console example is directly invocable.
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

    private static void Remember(ChatMessage message)
    {
        Backlog.Enqueue(message);
        while (Backlog.Count > BacklogSize && Backlog.TryDequeue(out _))
        {
            // Trim the backlog to its most recent entries.
        }
    }
}
