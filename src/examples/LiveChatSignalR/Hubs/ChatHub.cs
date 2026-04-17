using Bielu.AspNetCore.AsyncApi.Attributes.Attributes;
using LiveChatSignalR.Models;
using Microsoft.AspNetCore.SignalR;

namespace LiveChatSignalR.Hubs;

/// <summary>
/// SignalR hub for real-time live chat via WebSocket.
///
/// Clients connect to this hub to exchange messages inside named rooms,
/// send private (direct) messages to individual users, and receive
/// user-presence notifications when someone joins or leaves a room.
///
/// Channel layout:
///   chat/{roomId}    — room-scoped broadcast messages and presence events
///   chat/private     — private (direct) messages between two users
/// </summary>
[AsyncApi]
public class ChatHub(ILogger<ChatHub> logger) : Hub
{
    // -------------------------------------------------------------------------
    // Room channel  (chat/{roomId})
    // -------------------------------------------------------------------------

    /// <summary>
    /// Sends a message to everyone in the specified room.
    /// The server broadcasts a <see cref="ChatMessage"/> to the "ReceiveMessage"
    /// client method for all members of the room group.
    /// </summary>
    [Channel("chat/{roomId}",
        Description = "Room-scoped channel for broadcasting chat messages and presence events.",
        Servers = ["websocket"])]
    [ChannelParameter("roomId", typeof(string),
        Description = "Unique identifier of the chat room (e.g. \"general\", \"support\").")]
    [PublishOperation(typeof(ChatMessage), "Chat",
        OperationId = "SendRoomMessage",
        Summary = "Broadcast a message to all users in a chat room",
        Description = "The server receives a ChatMessage from the sender and pushes it to the 'ReceiveMessage' client method for every connected member of the room.")]
    public async Task SendRoomMessage(string roomId, string content)
    {
        var message = new ChatMessage
        {
            RoomId = roomId,
            SenderUsername = Context.User?.Identity?.Name ?? Context.ConnectionId,
            Content = content,
            SentAt = DateTime.UtcNow
        };

        logger.LogInformation(
            "User {User} sent message in room {Room}: {Content}",
            message.SenderUsername, roomId, content);

        await Clients.Group(roomId).SendAsync("ReceiveMessage", message);
    }

    /// <summary>
    /// Joins a chat room and notifies other members.
    /// The client is added to the SignalR group for the room, and a
    /// <see cref="UserPresenceEvent"/> with Action "Joined" is sent to the group.
    /// </summary>
    [Channel("chat/{roomId}",
        Description = "Room-scoped channel for broadcasting chat messages and presence events.",
        Servers = ["websocket"])]
    [ChannelParameter("roomId", typeof(string),
        Description = "Unique identifier of the chat room (e.g. \"general\", \"support\").")]
    [SubscribeOperation(typeof(UserPresenceEvent), "Chat",
        OperationId = "JoinRoom",
        Summary = "Join a chat room and receive presence and message events",
        Description = "Adds the caller to the room group. All room members (including the caller) receive a UserPresenceEvent confirming the join. Subsequently the caller receives ChatMessage events via 'ReceiveMessage'.")]
    public async Task JoinRoom(string roomId, string username)
    {
        await Groups.AddToGroupAsync(Context.ConnectionId, roomId);

        var presence = new UserPresenceEvent
        {
            RoomId = roomId,
            Username = username,
            Action = "Joined",
            OccurredAt = DateTime.UtcNow
        };

        logger.LogInformation("User {User} joined room {Room}", username, roomId);
        await Clients.Group(roomId).SendAsync("UserPresenceChanged", presence);
    }

    /// <summary>
    /// Leaves a chat room and notifies other members.
    /// The client is removed from the SignalR group for the room, and a
    /// <see cref="UserPresenceEvent"/> with Action "Left" is sent to the remaining members.
    /// </summary>
    [Channel("chat/{roomId}",
        Description = "Room-scoped channel for broadcasting chat messages and presence events.",
        Servers = ["websocket"])]
    [ChannelParameter("roomId", typeof(string),
        Description = "Unique identifier of the chat room (e.g. \"general\", \"support\").")]
    [SubscribeOperation(typeof(UserPresenceEvent), "Chat",
        OperationId = "LeaveRoom",
        Summary = "Leave a chat room",
        Description = "Removes the caller from the room group. Remaining room members receive a UserPresenceEvent with Action 'Left'.")]
    public async Task LeaveRoom(string roomId, string username)
    {
        await Groups.RemoveFromGroupAsync(Context.ConnectionId, roomId);

        var presence = new UserPresenceEvent
        {
            RoomId = roomId,
            Username = username,
            Action = "Left",
            OccurredAt = DateTime.UtcNow
        };

        logger.LogInformation("User {User} left room {Room}", username, roomId);
        await Clients.Group(roomId).SendAsync("UserPresenceChanged", presence);
    }

    // -------------------------------------------------------------------------
    // Private (direct-message) channel  (chat/private)
    // -------------------------------------------------------------------------

    /// <summary>
    /// Sends a private message from the caller to a specific recipient.
    /// The hub delivers the <see cref="PrivateMessage"/> only to the connection(s)
    /// that belong to the target user via the "ReceivePrivateMessage" client method.
    /// </summary>
    [Channel("chat/private",
        Description = "Channel for private (direct) messages between two users.",
        Servers = ["websocket"])]
    [PublishOperation(typeof(PrivateMessage), "Chat",
        OperationId = "SendPrivateMessage",
        Summary = "Send a private message to a specific user",
        Description = "The server delivers the PrivateMessage only to the recipient's active connection(s) via the 'ReceivePrivateMessage' client method. The sender also receives a confirmation copy.")]
    public async Task SendPrivateMessage(string recipientUsername, string content)
    {
        var senderUsername = Context.User?.Identity?.Name ?? Context.ConnectionId;

        var message = new PrivateMessage
        {
            SenderUsername = senderUsername,
            RecipientUsername = recipientUsername,
            Content = content,
            SentAt = DateTime.UtcNow
        };

        logger.LogInformation(
            "Private message from {Sender} to {Recipient}",
            senderUsername, recipientUsername);

        // In a real application you would look up the recipient's connection IDs
        // from a user-to-connection mapping (e.g. stored in Redis or in-memory).
        // Here we use a named group per user as a simplified stand-in.
        await Clients.Group($"user:{recipientUsername}").SendAsync("ReceivePrivateMessage", message);
        await Clients.Caller.SendAsync("ReceivePrivateMessage", message);
    }

    // -------------------------------------------------------------------------
    // Lifecycle overrides
    // -------------------------------------------------------------------------

    /// <summary>Registers the caller in a per-user group on connect.</summary>
    public override async Task OnConnectedAsync()
    {
        var username = Context.User?.Identity?.Name ?? Context.ConnectionId;
        await Groups.AddToGroupAsync(Context.ConnectionId, $"user:{username}");

        logger.LogInformation("Client {ConnectionId} ({User}) connected to ChatHub",
            Context.ConnectionId, username);

        await base.OnConnectedAsync();
    }

    /// <summary>Removes the caller from their per-user group on disconnect.</summary>
    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        var username = Context.User?.Identity?.Name ?? Context.ConnectionId;
        await Groups.RemoveFromGroupAsync(Context.ConnectionId, $"user:{username}");

        logger.LogInformation("Client {ConnectionId} ({User}) disconnected from ChatHub",
            Context.ConnectionId, username);

        await base.OnDisconnectedAsync(exception);
    }
}
