# Bielu.AspNetCore.AsyncApi.CloudEvents

Describes your AsyncAPI messages as [CloudEvents 1.0](https://cloudevents.io/) events, so the document
says what actually goes over the wire: the `id`, `source`, `specversion` and `type` attributes every
consumer relies on, named the way each protocol binding names them.

## Installation

```sh
dotnet add package Bielu.AspNetCore.AsyncApi.CloudEvents
```

## Usage

```csharp
builder.Services.AddAsyncApi(options => options
    .AddServer("kafka", "broker:9092", "kafka")
    .AddCloudEvents(cloudEvents => cloudEvents
        .WithSource("/orders")                 // pinned with const
        .WithTypePrefix("com.example.orders.") // type = prefix + message name
        .ConfigureMessage("orderCancelled", message => message.Type = "com.example.orders.cancelled.v2")));
```

- **Binary mode** (default): each message's headers gain the context attributes. With a Kafka server they
  are `ce_id`, `ce_source`, `ce_specversion`, `ce_type`, …; HTTP/WebSockets use `ce-`, AMQP
  `cloudEvents:`, and MQTT 5 user properties have no prefix. The prefix is chosen from the protocol of
  the servers each message's channels use. Existing headers are kept and combined with `allOf`.
- **Structured mode** (`UseMode(CloudEventsMode.Structured)`): the payload becomes the
  `application/cloudevents+json` envelope, with the original payload as `data`.

Message examples gain the attributes too, and the output validates against the official AsyncAPI 2.6.0
and 3.1.0 schemas.

## Documentation

- [CloudEvents](https://apidescriptions.bielu.pl/articles/cloudevents.html)
- [Full documentation](https://apidescriptions.bielu.pl/)

## Feedback & Contributing

Copyright (c) 2025 Arkadiusz Biel. Released under the [MIT license](https://licenses.nuget.org/MIT).
Bug reports and contributions are welcome at [the GitHub repository](https://github.com/bielu/Bielu.AspNetCore.ApiDescriptions).
