# Wolverine Integration

The `Bielu.AspNetCore.AsyncApi.Wolverine` package documents the messages
[Wolverine](https://wolverinefx.net) routes, reading the routing from the running Wolverine runtime
instead of from attributes. Whatever you configure with `ToKafkaTopic`, `ToSignalR` or
`ListenToKafkaTopic` is what the document describes, so the two can't drift apart.

## Installation

```bash
dotnet add package Bielu.AspNetCore.AsyncApi.Wolverine
```

## Configuration

Call `AddWolverine` on the options of each document that should include Wolverine messages:

```csharp
builder.Services.AddAsyncApi("realtime", options =>
{
    options.AddServer("signalr", "localhost", "signalr");
    options.AddWolverine(wolverine => wolverine
        .IncludeAssemblyContaining<OrderShipped>()
        .IncludeSchemes("signalr"));
});
```

| Option | Default | Purpose |
|--------|---------|---------|
| `IncludeAssembly` / `IncludeAssemblyContaining<T>` | Wolverine's application assembly | Assemblies whose public, concrete types are checked for routes |
| `IncludeSchemes` | Every scheme except `local`, `stub` and `tcp` | Which transports this document covers, e.g. `"signalr"` or `"kafka"` |
| `UseServer(scheme, serverName)` | The scheme itself | The AsyncAPI server channels on that transport link to (it must be registered with `AddServer`) |
| `MapChannel(endpointUri, address)` | Derived from the endpoint | Overrides a channel address |
| `SignalRClientMethod` | `ReceiveMessage` | The client method SignalR operations target |

## How the document is built

For each candidate message type, every Wolverine route to an included transport becomes a `send`
operation on the channel of the route's endpoint. Every listening endpoint with a default incoming
message type (`DefaultIncomingMessage<T>()`) becomes a `receive` operation. A message published to two
transports appears once in `components/messages`, with an operation per transport.

Messages are named with Wolverine's message type name, which is what goes on the wire: the
`message-type` header on brokers, and the CloudEvents `type` on SignalR. Give public messages an explicit
`[MessageIdentity("orders.order-shipped")]` so renaming the class doesn't change the contract.

### Channel addresses

- **Kafka and other brokers**: the topic or queue name from the endpoint URI (`kafka://topic/orders`
  becomes `orders`)
- **SignalR**: Wolverine's SignalR transport has a single endpoint, `signalr://wolverine/`, which
  doesn't name the hub. The package looks up the path the `WolverineHub` is mapped at with
  `MapWolverineSignalRHub`, and fails with a clear message if it finds none or several; use
  `MapChannel(new Uri("signalr://wolverine/"), "/hubs/chat")` in that case

### SignalR messages

Wolverine's SignalR transport delivers every message on the `ReceiveMessage` client method as a
CloudEvents JSON document: `type` is the message name and `data` the payload. The document's message
payloads describe `data`.

The transport serializes with its own `JsonSerializerOptions`, not ASP.NET Core's `JsonOptions`, while
the document's schemas are generated from `JsonOptions`. If you customise JSON (enum naming, number
handling), pass the same settings to the transport with
`opts.UseSignalR<THub>().OverrideJson(options)` so the wire matches the document.

## Combining with attributes

`AddWolverine` runs as a document transformer, after attribute discovery. Integrations that don't go
through Wolverine can keep using `[AsyncApi]`, `[Channel]` and the operation attributes; both end up in
the same document, and channels with the same address are merged.
