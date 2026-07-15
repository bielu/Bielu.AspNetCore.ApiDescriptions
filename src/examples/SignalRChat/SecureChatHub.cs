using System.Collections.Concurrent;
using Bielu.AspNetCore.AsyncApi.Attributes.Attributes;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using SignalRChat.Auth;

namespace SignalRChat;

/// <summary>
/// Strongly-typed server-to-client contract for the secured hub.
/// </summary>
[AsyncApi("signalr-secure")]
[Channel("secureChatHub", Description = "Real-time chat hub requiring API key authentication.")]
public interface ISecureChatClient
{
    /// <summary>Pushes a chat message to connected clients.</summary>
    [SubscribeOperation(typeof(ChatMessage), "chat", Summary = "A chat message was broadcast.", BindingsRef = "secureReceiveMessage")]
    Task ReceiveMessage(ChatMessage message);

    /// <summary>Notifies clients that a user joined the hub.</summary>
    [SubscribeOperation(typeof(PresenceEvent), "presence", Summary = "A user connected to the hub.", BindingsRef = "secureUserJoined")]
    Task UserJoined(PresenceEvent presence);

    /// <summary>Notifies clients that a user left the hub.</summary>
    [SubscribeOperation(typeof(PresenceEvent), "presence", Summary = "A user disconnected from the hub.", BindingsRef = "secureUserLeft")]
    Task UserLeft(PresenceEvent presence);
}

/// <summary>
/// A SignalR hub that mirrors <see cref="ChatHub"/> but requires an API key to connect. It exists
/// solely as an auth-integration example: authenticate via Scalar's Authentication panel using the
/// demo key <c>signalr-demo-key</c>, then press Connect in the SignalR console.
/// </summary>
[Authorize]
[AsyncApi("signalr-secure")]
[Channel("secureChatHub", BindingsRef = "secureChatHub", Description = "Real-time chat hub requiring API key authentication.")]
public class SecureChatHub : Hub<ISecureChatClient>
{
    private static readonly ConcurrentDictionary<string, string> Connections = new();

    private string UserName =>
        Context.GetHttpContext()?.Request.Query["user"].ToString() is { Length: > 0 } name
            ? name
            : $"anon-{Context.ConnectionId[..Math.Min(6, Context.ConnectionId.Length)]}";

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

    /// <summary>Broadcasts a message to everyone connected to the hub.</summary>
    [PublishOperation(typeof(ChatMessage), "chat", Summary = "Broadcast a chat message to every connected client.", BindingsRef = "secureSendMessage")]
    public Task SendMessage(ChatMessage message)
        => Clients.All.ReceiveMessage(message with { Room = null });

    /// <summary>Returns the display names of all currently connected clients.</summary>
    [PublishOperation(Summary = "Return the users currently connected to the hub.", BindingsRef = "secureGetOnlineUsers")]
    [Channel("secureChatHub")]
    public Task<IReadOnlyCollection<string>> GetOnlineUsers()
        => Task.FromResult<IReadOnlyCollection<string>>(Connections.Values.Distinct().ToArray());
}
