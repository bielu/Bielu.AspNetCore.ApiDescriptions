using Bielu.AspNetCore.AsyncApi.Attributes.Attributes;
using Microsoft.AspNetCore.SignalR;

namespace AsyncApiSignalR;

[AsyncApi]
[Channel("/chat")]
public class ChatHub : Hub
{
    /// <summary>
    /// Sends a message to all connected clients.
    /// </summary>
    /// <param name="user">The name of the user sending the message.</param>
    /// <param name="message">The message content.</param>
    [PublishOperation(typeof(ChatMessage), "SendMessage", Summary = "Send a message to the chat.")]
    public async Task SendMessage(string user, string message)
    {
        await Clients.All.SendAsync("ReceiveMessage", user, message);
    }

    /// <summary>
    /// Client-side handler for receiving chat messages.
    /// </summary>
    /// <param name="message">The received message.</param>
    [SubscribeOperation(typeof(ChatMessage), "ReceiveMessage", Summary = "Receive a message from the chat.")]
    public void OnReceiveMessage(ChatMessage message) { }
}

/// <summary>
/// Represents a chat message.
/// </summary>
/// <param name="User">The sender's name.</param>
/// <param name="Message">The message text.</param>
public record ChatMessage(string User, string Message);
