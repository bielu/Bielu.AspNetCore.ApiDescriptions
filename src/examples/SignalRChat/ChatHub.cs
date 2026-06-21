using Bielu.AspNetCore.AsyncApi.Attributes.Attributes;
using Microsoft.AspNetCore.SignalR;

namespace SignalRChat;

/// <summary>A chat message exchanged over the hub.</summary>
public record ChatMessage(string User, string Text, DateTimeOffset SentAt);

/// <summary>Strongly-typed client contract for server-to-client pushes.</summary>
public interface IChatClient
{
    /// <summary>Pushes a chat message to connected clients.</summary>
    Task ReceiveMessage(ChatMessage message);
}

/// <summary>
/// A real ASP.NET Core SignalR hub. It is also annotated with AsyncAPI attributes so the document
/// generator surfaces it as the <c>chatHub</c> channel; the SignalR protocol bindings are linked to
/// that channel/operation via the <c>BindingsRef</c> values registered in <c>Program.cs</c>.
/// </summary>
[AsyncApi]
[Channel("chatHub", BindingsRef = "chatHub", Description = "Real-time chat hub backed by ASP.NET Core SignalR.")]
public class ChatHub : Hub<IChatClient>
{
    /// <summary>Client-to-server invocation: a client sends a message to the hub.</summary>
    [PublishOperation(typeof(ChatMessage), "chat", Summary = "Send a chat message to the hub.", BindingsRef = "sendMessage")]
    public async Task SendMessage(ChatMessage message)
    {
        await Clients.All.ReceiveMessage(message);
    }

    /// <summary>Adds the caller to a named chat group.</summary>
    [PublishOperation(Summary = "Join a named chat group.")]
    [Channel("chatHub")]
    public Task JoinGroup(string groupName)
        => Groups.AddToGroupAsync(Context.ConnectionId, groupName);
}
