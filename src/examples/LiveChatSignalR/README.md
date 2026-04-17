# Live Chat SignalR Example

This example shows how to document a real-time **live-chat** service built with
**ASP.NET Core SignalR** using the `Bielu.AspNetCore.AsyncApi` library.

Both **group rooms** (e.g. `general`, `support`) and **private 1-on-1
conversations** share the same channel and logic — the only difference is the
`chatId` used to identify the conversation.

## Features

| Channel | Description |
|---|---|
| `chat/{chatId}` | All chat messages and user-presence events (rooms **and** private conversations) |

## Authentication

The hub requires an authenticated user. The example is configured with
**JWT Bearer** authentication — clients must provide a valid bearer token when
connecting:

```js
const connection = new signalR.HubConnectionBuilder()
    .withUrl("/hubs/chat", { accessTokenFactory: () => token })
    .build();
```

The authenticated user's `Name` claim is used as the username for all chat
operations (message sender, presence events). Clients do not supply their own
username.

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
| `JoinChat` | `chatId` | Join a chat and start receiving its messages |
| `LeaveChat` | `chatId` | Leave a chat |
| `SendMessage` | `SendMessageRequest` | Send a message to a chat (request contains `chatId` and `content`) |

## Client methods (server → client)

| Method | Payload | Description |
|---|---|---|
| `ReceiveMessage` | `ChatMessage` | A new message was sent in the chat |
| `UserPresenceChanged` | `UserPresenceEvent` | A user joined or left the chat |

## AsyncAPI document

Running `dotnet run` and calling `GET /asyncapi/v1.json` produces a document
similar to the one below.

```json
{
  "asyncapi": "2.6.0",
  "info": {
    "title": "Live Chat API",
    "version": "1.0.0",
    "description": "A live-chat service built with ASP.NET Core SignalR. Clients connect over WebSocket to join chats, send messages, and receive user-presence events in real time. Group rooms and private conversations use the same channel.",
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
    "chat/{chatId}": {
      "description": "Channel for all chat messages and presence events. Works for both group rooms and private conversations.",
      "servers": ["websocket"],
      "parameters": {
        "chatId": {
          "description": "Unique identifier of the chat (e.g. \"general\", \"support\", or a private conversation ID).",
          "schema": { "type": "string" }
        }
      },
      "publish": {
        "operationId": "SendMessage",
        "summary": "Send a message to a chat",
        "description": "The server receives a SendMessageRequest, wraps it in a ChatMessage and pushes it to the 'ReceiveMessage' client method for every member of the chat.",
        "tags": [{ "name": "Chat" }],
        "message": { "$ref": "#/components/messages/chatMessage" }
      },
      "subscribe": {
        "operationId": "JoinChat",
        "summary": "Join a chat and receive messages and presence events",
        "description": "Adds the caller to the chat group. All chat members (including the caller) receive a UserPresenceEvent confirming the join.",
        "tags": [{ "name": "Chat" }],
        "message": { "$ref": "#/components/messages/userPresenceEvent" }
      }
    }
  },
  "components": {
    "schemas": {
      "chatMessage": {
        "title": "chatMessage",
        "type": "object",
        "properties": {
          "chatId":           { "type": "string" },
          "senderUsername":   { "type": "string" },
          "content":          { "type": "string" },
          "sentAt":           { "type": "string", "format": "dateTime" }
        }
      },
      "userPresenceEvent": {
        "title": "userPresenceEvent",
        "type": "object",
        "properties": {
          "chatId":      { "type": "string" },
          "username":    { "type": "string" },
          "action":      { "type": "string", "description": "\"Joined\" or \"Left\"" },
          "occurredAt":  { "type": "string", "format": "dateTime" }
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
      }
    }
  }
}
```

## How it works

1. **`[AsyncApi]`** on `ChatHub` tells the library to scan this class for channel declarations.
2. **`[Channel("chat/{chatId}", ...)]`** declares a parameterised channel. The `{chatId}` placeholder is described by `[ChannelParameter]`.
3. **`[PublishOperation]`** on `SendMessage` documents the operation where the *server publishes* a `ChatMessage` to all chat members.
4. **`[SubscribeOperation]`** on `JoinChat` / `LeaveChat` documents that *clients subscribe* to `UserPresenceEvent` messages from the server.

The same `[Channel]` name used on multiple methods is merged into a single channel entry in the generated document; publish and subscribe operations are collected from each decorated method independently.

Group rooms and private conversations share the exact same model — the `chatId` is the only distinguishing factor. For private conversations you would typically generate a deterministic chat ID from the two participant usernames (e.g. `private:alice+bob`).
