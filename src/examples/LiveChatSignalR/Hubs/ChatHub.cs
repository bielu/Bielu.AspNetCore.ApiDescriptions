using Bielu.AspNetCore.AsyncApi.Attributes.Attributes;
using LiveChatSignalR.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;

namespace LiveChatSignalR.Hubs;

/// <summary>
/// SignalR hub for real-time live chat via WebSocket.
///
/// Clients connect to this hub to exchange messages inside named chats
/// and receive user-presence notifications when someone joins or leaves.
/// Both group rooms (e.g. "general") and private 1-on-1 conversations
/// use the same channel and the same logic — only the chatId differs.
///
/// Channel layout:
///   chat/{chatId}  — all chat messages and presence events
/// </summary>
[AsyncApi]
[Authorize]
public class ChatHub(ILogger<ChatHub> logger) : Hub
{
    // -------------------------------------------------------------------------
    // Send message  (chat/{chatId})
    // -------------------------------------------------------------------------

    /// <summary>
    /// Sends a message to everyone in the specified chat.
    /// The server broadcasts a <see cref="ChatMessage"/> to the "ReceiveMessage"
    /// client method for all members of the chat group.
    /// </summary>
    [Channel("chat/{chatId}",
        Description = "Channel for all chat messages and presence events. Works for both group rooms and private conversations.",
        Servers = ["websocket"])]
    [ChannelParameter("chatId", typeof(string),
        Description = "Unique identifier of the chat (e.g. \"general\", \"support\", or a private conversation ID).")]
    [PublishOperation(typeof(ChatMessage), "Chat",
        OperationId = "SendMessage",
        Summary = "Send a message to a chat",
        Description = "The server receives a SendMessageRequest, wraps it in a ChatMessage and pushes it to the 'ReceiveMessage' client method for every member of the chat.")]
    public async Task SendMessage(SendMessageRequest request)
    {
        var message = new ChatMessage
        {
            ChatId = request.ChatId,
            SenderUsername = Context.User!.Identity!.Name!,
            Content = request.Content,
            SentAt = DateTime.UtcNow
        };

        logger.LogInformation(
            "User {User} sent message in chat {Chat}: {Content}",
            message.SenderUsername, request.ChatId, request.Content);

        await Clients.Group(request.ChatId).SendAsync("ReceiveMessage", message);
    }

    // -------------------------------------------------------------------------
    // Join / Leave  (chat/{chatId})
    // -------------------------------------------------------------------------

    /// <summary>
    /// Joins a chat and notifies other members.
    /// The client is added to the SignalR group for the chat, and a
    /// <see cref="UserPresenceEvent"/> with Action "Joined" is sent to the group.
    /// </summary>
    [Channel("chat/{chatId}",
        Description = "Channel for all chat messages and presence events. Works for both group rooms and private conversations.",
        Servers = ["websocket"])]
    [ChannelParameter("chatId", typeof(string),
        Description = "Unique identifier of the chat (e.g. \"general\", \"support\", or a private conversation ID).")]
    [SubscribeOperation(typeof(UserPresenceEvent), "Chat",
        OperationId = "JoinChat",
        Summary = "Join a chat and receive messages and presence events",
        Description = "Adds the caller to the chat group. All chat members (including the caller) receive a UserPresenceEvent confirming the join. Subsequently the caller receives ChatMessage events via 'ReceiveMessage'.")]
    public async Task JoinChat(string chatId)
    {
        var username = Context.User!.Identity!.Name!;
        await Groups.AddToGroupAsync(Context.ConnectionId, chatId);

        var presence = new UserPresenceEvent
        {
            ChatId = chatId,
            Username = username,
            Action = "Joined",
            OccurredAt = DateTime.UtcNow
        };

        logger.LogInformation("User {User} joined chat {Chat}", username, chatId);
        await Clients.Group(chatId).SendAsync("UserPresenceChanged", presence);
    }

    /// <summary>
    /// Leaves a chat and notifies other members.
    /// The client is removed from the SignalR group for the chat, and a
    /// <see cref="UserPresenceEvent"/> with Action "Left" is sent to the remaining members.
    /// </summary>
    [Channel("chat/{chatId}",
        Description = "Channel for all chat messages and presence events. Works for both group rooms and private conversations.",
        Servers = ["websocket"])]
    [ChannelParameter("chatId", typeof(string),
        Description = "Unique identifier of the chat (e.g. \"general\", \"support\", or a private conversation ID).")]
    [SubscribeOperation(typeof(UserPresenceEvent), "Chat",
        OperationId = "LeaveChat",
        Summary = "Leave a chat",
        Description = "Removes the caller from the chat group. Remaining chat members receive a UserPresenceEvent with Action 'Left'.")]
    public async Task LeaveChat(string chatId)
    {
        var username = Context.User!.Identity!.Name!;
        await Groups.RemoveFromGroupAsync(Context.ConnectionId, chatId);

        var presence = new UserPresenceEvent
        {
            ChatId = chatId,
            Username = username,
            Action = "Left",
            OccurredAt = DateTime.UtcNow
        };

        logger.LogInformation("User {User} left chat {Chat}", username, chatId);
        await Clients.Group(chatId).SendAsync("UserPresenceChanged", presence);
    }

    // -------------------------------------------------------------------------
    // Lifecycle overrides
    // -------------------------------------------------------------------------

    /// <summary>Called when a new client connects to the hub.</summary>
    public override async Task OnConnectedAsync()
    {
        var username = Context.User!.Identity!.Name!;

        logger.LogInformation("Client {ConnectionId} ({User}) connected to ChatHub",
            Context.ConnectionId, username);

        await base.OnConnectedAsync();
    }

    /// <summary>Called when a client disconnects from the hub.</summary>
    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        var username = Context.User!.Identity!.Name!;

        logger.LogInformation("Client {ConnectionId} ({User}) disconnected from ChatHub",
            Context.ConnectionId, username);

        await base.OnDisconnectedAsync(exception);
    }
}
