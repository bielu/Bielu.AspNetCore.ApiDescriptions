# Bielu.AspNetCore.AsyncApi.Wolverine

[Wolverine](https://wolverinefx.net) integration for
[Bielu.AspNetCore.AsyncApi](https://www.nuget.org/packages/Bielu.AspNetCore.AsyncApi).

Wolverine already knows where every message goes: its routing rules decide which messages are
published to Kafka topics, pushed to SignalR clients or read from listeners. This package builds the
AsyncAPI document from that routing, so channels and operations follow the configuration instead of
being repeated in `[Channel]`/`[PublishOperation]` attributes that can drift from it.

## Installation

```sh
dotnet add package Bielu.AspNetCore.AsyncApi.Wolverine
```

## Usage

```csharp
builder.Host.UseWolverine(opts =>
{
    opts.UseSignalR<NotificationsHub>();
    opts.Publish(rule => rule.MessagesImplementing<WebSocketMessage>().ToSignalR());

    opts.UseKafka(kafkaBootstrap);
    opts.PublishMessage<OrderPlaced>().ToKafkaTopic("orders");
    opts.ListenToKafkaTopic("stock").DefaultIncomingMessage<StockChanged>();
});

// One document per audience: what the browser receives, and what other services exchange.
builder.Services.AddAsyncApi("realtime", options =>
{
    options.AddServer("signalr", "localhost", "signalr");
    options.AddWolverine(wolverine => wolverine
        .IncludeAssemblyContaining<OrderShipped>()
        .IncludeSchemes("signalr"));
});

builder.Services.AddAsyncApi("messaging", options =>
{
    options.AddServer("kafka", kafkaBootstrap, "kafka");
    options.AddWolverine(wolverine => wolverine
        .IncludeAssemblyContaining<OrderPlaced>()
        .IncludeSchemes("kafka"));
});
```

What it generates:

| Wolverine | AsyncAPI |
|-----------|----------|
| A route for a message type (`ToKafkaTopic`, `ToSignalR`, ...) | A `send` operation on the endpoint's channel |
| A listener with `DefaultIncomingMessage<T>()` | A `receive` operation on the listener's channel |
| The message type name (`[MessageIdentity]`, or Wolverine's default) | The message name, the same one used on the wire |
| The SignalR transport | A channel at the path the `WolverineHub` is mapped at, with `signalr` bindings targeting `ReceiveMessage` |
| A Kafka topic | A channel at the topic, with a `kafka` channel binding |

In-process routes (`local://`) are never documented. XML documentation comments on the message types
become the message and schema descriptions; the package loads the XML files of the assemblies it scans.

## Documentation

- [Wolverine integration](https://apidescriptions.bielu.pl/articles/wolverine.html)
- [Full documentation](https://apidescriptions.bielu.pl/)

## Feedback & Contributing

Released under the [MIT license](https://licenses.nuget.org/MIT). Bug reports and contributions are
welcome at [the GitHub repository](https://github.com/bielu/Bielu.AspNetCore.ApiDescriptions).
