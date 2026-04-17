# Live Chat SignalR Example

This example shows how to document a real-time **live-chat** service built with
**ASP.NET Core SignalR** using the `Bielu.AspNetCore.AsyncApi` library.

## Features

| Channel | Description |
|---|---|
| `chat/{roomId}` | Room-scoped broadcast — send and receive chat messages, and user presence (join/leave) events |
| `chat/private` | Private (direct) messages between two users |

## Running

```sh
cd src/examples/LiveChatSignalR
dotnet run
```

The AsyncAPI documentation is available at:

| Endpoint | URL |
|---|---|
| AsyncAPI JSON | <http://localhost:5000/asyncapi/v1.json> |
| AsyncAPI UI | <http://localhost:5000/asyncapi> |
| SignalR hub | `ws://localhost:5000/hubs/chat` |

## Hub methods (client → server)

| Method | Parameters | Description |
|---|---|---|
| `JoinRoom` | `roomId`, `username` | Join a room and start receiving its messages |
| `LeaveRoom` | `roomId`, `username` | Leave a room |
| `SendRoomMessage` | `SendMessageRequest` | Broadcast a message to the room |
| `SendPrivateMessage` | `SendPrivateMessageRequest` | Send a private message to a specific user |

## Client methods (server → client)

| Method | Payload | Description |
|---|---|---|
| `ReceiveMessage` | `ChatMessage` | A new message was broadcast to the room |
| `UserPresenceChanged` | `UserPresenceEvent` | A user joined or left the room |
| `ReceivePrivateMessage` | `PrivateMessage` | A private message was delivered |

## AsyncAPI document

Running `dotnet run` and calling `GET /asyncapi/v1.json` produces a document
similar to the one below.

```json
{
  "asyncapi": "2.6.0",
  "info": {
    "title": "Live Chat API",
    "version": "1.0.0",
    "description": "A live-chat service built with ASP.NET Core SignalR. Clients connect over WebSocket to join rooms, broadcast messages, send private messages, and receive user-presence events in real time.",
    "license": {
      "name": "Apache 2.0",
      "url": "https://www.apache.org/licenses/LICENSE-2.0"
    }
  },
  "servers": {
    "websocket": {
      "url": "localhost:5000",
      "protocol": "ws",
      "description": "WebSocket server — real-time live chat via SignalR"
    }
  },
  "defaultContentType": "application/json",
  "channels": {
    "chat/{roomId}": {
      "description": "Room-scoped channel for broadcasting chat messages and presence events.",
      "servers": ["websocket"],
      "parameters": {
        "roomId": {
          "description": "Unique identifier of the chat room (e.g. \"general\", \"support\").",
          "schema": { "type": "string" }
        }
      },
      "publish": {
        "operationId": "SendRoomMessage",
        "summary": "Broadcast a message to all users in a chat room",
        "description": "The server receives a SendMessageRequest from the sender, wraps it in a ChatMessage and pushes it to the 'ReceiveMessage' client method for every connected member of the room.",
        "tags": [{ "name": "Chat" }],
        "message": { "$ref": "#/components/messages/chatMessage" }
      },
      "subscribe": {
        "operationId": "JoinRoom",
        "summary": "Join a chat room and receive presence and message events",
        "description": "Adds the caller to the room group. All room members (including the caller) receive a UserPresenceEvent confirming the join.",
        "tags": [{ "name": "Chat" }],
        "message": { "$ref": "#/components/messages/userPresenceEvent" }
      }
    },
    "chat/private": {
      "description": "Channel for private (direct) messages between two users.",
      "servers": ["websocket"],
      "publish": {
        "operationId": "SendPrivateMessage",
        "summary": "Send a private message to a specific user",
        "description": "The server delivers the PrivateMessage only to the recipient's active connection(s) via the 'ReceivePrivateMessage' client method.",
        "tags": [{ "name": "Chat" }],
        "message": { "$ref": "#/components/messages/privateMessage" }
      }
    }
  },
  "components": {
    "schemas": {
      "chatMessage": {
        "title": "chatMessage",
        "type": "object",
        "properties": {
          "roomId":          { "type": "string" },
          "senderUsername":  { "type": "string" },
          "content":         { "type": "string" },
          "sentAt":          { "type": "string", "format": "dateTime" }
        }
      },
      "userPresenceEvent": {
        "title": "userPresenceEvent",
        "type": "object",
        "properties": {
          "roomId":      { "type": "string" },
          "username":    { "type": "string" },
          "action":      { "type": "string", "description": "\"Joined\" or \"Left\"" },
          "occurredAt":  { "type": "string", "format": "dateTime" }
        }
      },
      "privateMessage": {
        "title": "privateMessage",
        "type": "object",
        "properties": {
          "senderUsername":    { "type": "string" },
          "recipientUsername": { "type": "string" },
          "content":           { "type": "string" },
          "sentAt":            { "type": "string", "format": "dateTime" }
        }
      }
    },
    "messages": {
      "chatMessage": {
        "name": "chatMessage",
        "title": "chatMessage",
        "payload": { "$ref": "#/components/schemas/chatMessage" }
      },
      "userPresenceEvent": {
        "name": "userPresenceEvent",
        "title": "userPresenceEvent",
        "payload": { "$ref": "#/components/schemas/userPresenceEvent" }
      },
      "privateMessage": {
        "name": "privateMessage",
        "title": "privateMessage",
        "payload": { "$ref": "#/components/schemas/privateMessage" }
      }
    }
  }
}
```

## How it works

1. **`[AsyncApi]`** on `ChatHub` tells the library to scan this class for channel declarations.
2. **`[Channel("chat/{roomId}", ...)]`** declares a parameterised channel. The `{roomId}` placeholder is described by `[ChannelParameter]`.
3. **`[PublishOperation]`** on `SendRoomMessage` documents the operation where the *server publishes* (broadcasts) a `ChatMessage` to clients.
4. **`[SubscribeOperation]`** on `JoinRoom` / `LeaveRoom` documents that the *client subscribes* to `UserPresenceEvent` messages from the server.
5. **`[PublishOperation]`** on `SendPrivateMessage` documents the private-message channel.

The same `[Channel]` name used on multiple methods is merged into a single channel entry in the generated document; publish and subscribe operations are collected from each decorated method independently.
