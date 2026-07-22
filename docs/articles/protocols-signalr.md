# SignalR Protocol

The `Bielu.AspNetCore.AsyncApi.Extensions.Protocols.SignalR` package adds support for documenting ASP.NET Core SignalR hubs using the custom `signalr` protocol.

## Installation

```bash
dotnet add package Bielu.AspNetCore.AsyncApi.Extensions.Protocols.SignalR
```

## Configuration

Register SignalR bindings in `AddAsyncApi`:

```csharp
builder.Services.AddAsyncApi(options =>
{
    options.AddChannelBinding("chatHub", new SignalRChannelBinding
    {
        Hub = "/chatHub",
        Transports = { SignalRProtocol.Transports.WebSockets, SignalRProtocol.Transports.LongPolling },
        Protocols = { SignalRProtocol.HubProtocols.Json, SignalRProtocol.HubProtocols.MessagePack },
    });

    options.AddOperationBinding("sendMessage", new SignalROperationBinding
    {
        Target = "SendMessage",
        Direction = SignalRProtocol.Directions.ClientToServer,
        CallType = SignalRProtocol.CallTypes.Invocation,
    });
});
```

## Hub Annotation

Annotate your hub class and its methods:

```csharp
[AsyncApi]
[Channel("chatHub", BindingsRef = "chatHub")]
public class ChatHub : Hub
{
    [PublishOperation(typeof(ChatMessage), BindingsRef = "sendMessage")]
    public Task SendMessage(ChatMessage message) => Clients.All.SendAsync("ReceiveMessage", message);
}
```

## Message Types

SignalR messages have specific types corresponding to the hub protocol:

| Token | Wire id | Meaning |
| --- | --- | --- |
| `invocation` | 1 | Client/server invokes a method |
| `streamItem` | 2 | A single item of a stream |
| `completion` | 3 | Result/error of an invocation |
| `streamInvocation` | 4 | Invokes a streaming method |
| `cancelInvocation` | 5 | Cancels a streaming invocation |
| `ping` | 6 | Keep-alive |
| `close` | 7 | Connection closing |

## Interactive Console

To enable the live SignalR console in Scalar, see the [Scalar Consoles](scalar-consoles.md) guide.
