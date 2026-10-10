# Broker Bindings (Kafka, AMQP, MQTT, HTTP, WebSockets, Pulsar, SNS, SQS)

Bindings for these protocols come from the
[`ByteBard.AsyncAPI.NET.Bindings`](https://www.nuget.org/packages/ByteBard.AsyncAPI.NET.Bindings/) package.
`Bielu.AspNetCore.AsyncApi` does not reference it for you, so add it to the project that configures the
document:

```bash
dotnet add package ByteBard.AsyncAPI.NET.Bindings
```

## Available bindings

| Protocol | Server | Channel | Operation | Message |
| --- | --- | --- | --- | --- |
| AMQP | | `AMQPChannelBinding` | `AMQPOperationBinding` | `AMQPMessageBinding` |
| HTTP | | | `HttpOperationBinding` | `HttpMessageBinding` |
| Kafka | `KafkaServerBinding` | `KafkaChannelBinding` | `KafkaOperationBinding` | `KafkaMessageBinding` |
| MQTT | `MQTTServerBinding` | | `MQTTOperationBinding` | `MQTTMessageBinding` |
| Pulsar | `PulsarServerBinding` | `PulsarChannelBinding` | | |
| SNS | | `SnsChannelBinding` | `SnsOperationBinding` | |
| SQS | | `SqsChannelBinding` | `SqsOperationBinding` | |
| WebSockets | | `WebSocketsChannelBinding` | | |

SignalR, gRPC, SSE and WebRTC bindings ship in this repository's own packages; see the other pages in this
section.

## Registering bindings

Each binding level has a method on `AsyncApiOptions`. The first argument is the name the binding is
registered under in the document's `components`.

```csharp
builder.Services.AddAsyncApi(options =>
{
    options.AddServer("broker", "localhost:9092", "kafka");
    options.AddServerBinding("broker", new KafkaServerBinding { SchemaRegistryVendor = "confluent" });

    options.AddChannelBinding("orders", new KafkaChannelBinding { Topic = "orders", Partitions = 3 });

    options.AddOperationBinding("publishOrder", new KafkaOperationBinding
    {
        GroupId = new AsyncApiJsonSchema { Type = SchemaType.String },
    });

    options.AddMessageBinding("orderPlaced", new KafkaMessageBinding { SchemaLookupStrategy = "TopicIdStrategy" });
});
```

| Method | Registered in | Attached to |
| --- | --- | --- |
| `AddServerBinding` | `components/serverBindings` | The server added with `AddServer` under the same name |
| `AddChannelBinding` | `components/channelBindings` | The channel of the same name, or any `[Channel(BindingsRef = "...")]` |
| `AddOperationBinding` | `components/operationBindings` | Any operation attribute with a matching `BindingsRef` |
| `AddMessageBinding` | `components/messageBindings` | Any `[Message(BindingsRef = "...")]` |

```csharp
[AsyncApi]
public class OrderEvents
{
    [Channel("orders", BindingsRef = "orders")]
    [Message(typeof(OrderPlaced), BindingsRef = "orderPlaced")]
    [PublishOperation(typeof(OrderPlaced), BindingsRef = "publishOrder")]
    public void PublishOrder(OrderPlaced order) { }
}
```

A `BindingsRef` that matches no registered name is ignored: the element is generated without bindings.

Calling an `Add…Binding` method again with the same name adds a second binding under it, which is how one
channel documents more than one protocol.

## Known upstream issues

These come from `ByteBard.AsyncAPI.NET.Bindings` 3.0.1, not from this library. Each one produces a document
that the official AsyncAPI JSON Schema rejects:

- **WebSockets:** the binding is written under the key `websockets`; the AsyncAPI bindings specification
  uses `ws`.
- **Kafka message binding:** `SchemaIdLocation` is written as `SchemaIdLocation` instead of
  `schemaIdLocation`.
- **SQS:** `fifoQueue` is required by the schema but is omitted when `Queue.FifoQueue` is `false`, so a
  standard (non-FIFO) queue serializes without it.

Until they are fixed upstream, a transformer registered with
`AsyncApiOptions.AddSerializedDocumentTransformer` can correct the JSON before it is served.
