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
| `IncludeReplies()` | Off | Also documents a returned message as a `reply` when it has a route of its own; unrouted ones always are (AsyncAPI 3.x) |

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

### Replies

A Wolverine handler can return a message. Every request carries a `reply-uri` header, and when it also
names the returned type in its `reply-requested` header, as `InvokeAsync<Quote>(new GetQuote(sku))` does,
Wolverine sends the returned message to that address instead of publishing it. Otherwise the message is
published through the routing rules like any other.

The handler itself does not say which of the two is meant, so the package goes by the routing:

- A returned message with **no route** can only ever leave the handler as a reply, so it is always
  documented as one
- A returned message that **has a route** may be a reply or an ordinary published event. It is
  documented as a reply only when you opt in:

```csharp
options.AddWolverine(wolverine => wolverine.IncludeReplies());
```

A `receive` operation with a reply gets:

- `reply.address.location` set to `$message.header#/reply-uri`
- `reply.channel` pointing at a channel of its own, named after the operation
  (`receive.quotes.get-quote.reply`) and without an address, because the address is only known from the
  request
- `reply.messages` listing the returned message types

Only listeners with a default incoming message type have a `receive` operation, so only their handlers
are covered. AsyncAPI 2.x has no reply object, so a 2.x document is left unchanged.

## Spec metadata

Routing doesn't say everything a document can: how to correlate a request with a reply, where to read
more, which traits apply, who may call. Declare those on the Wolverine options:

```csharp
builder.Services.AddAsyncApi("realtime", options =>
{
    options.AddMessageTrait("traced", new AsyncApiMessageTrait { ContentType = "application/json" });
    options.AddOperationTrait("audited", new AsyncApiOperationTrait { Summary = "Audited operation" });
    options.AddSecurityScheme("apiKey", new AsyncApiSecurityScheme { Type = SecuritySchemeType.ApiKey, In = ParameterLocation.User });

    options.AddWolverine(wolverine => wolverine
        .IncludeCorrelationId()
        .WithExternalDocs<OrderPlaced>("https://example.com/orders", "Order events")
        .WithMessageTraits<OrderPlaced>("traced")
        .WithOperationTraits<OrderPlaced>("audited")
        .RequireSecurity<OrderPlaced>("apiKey"));
});
```

| Method | Effect |
|--------|--------|
| `IncludeCorrelationId()` | Every message sent through a broker gets a `correlationId` at `$message.header#/correlation-id`, the header Wolverine stamps on each envelope it sends that way. SignalR messages are left out: that transport sends a CloudEvents JSON wrapper, not this header |
| `WithExternalDocs<T>(url, description)` | `externalDocs` on the message for `T`. `url` must be absolute, as AsyncAPI requires |
| `WithMessageTraits<T>(keys)` | `traits` references on the message for `T` |
| `WithOperationTraits<T>(keys)` | `traits` references on every operation of `T` |
| `RequireSecurity<T>(keys)` | `security` references on every operation of `T` |

Traits and security schemes are registered on the document's own options (`AddMessageTrait`,
`AddOperationTrait`, `AddSecurityScheme`), exactly as for attribute-declared documents; the Wolverine
methods only reference them by key.

## Combining with attributes

`AddWolverine` runs as a document transformer, after attribute discovery. Integrations that don't go
through Wolverine can keep using `[AsyncApi]`, `[Channel]` and the operation attributes; both end up in
the same document, and channels with the same address are merged.
