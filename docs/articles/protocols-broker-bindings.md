# Broker Bindings (Kafka, AMQP, MQTT, HTTP, WebSockets, Pulsar, SNS, SQS, JMS, IBM MQ, Solace, NATS and more)

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
| Anypoint MQ ¹ | | `AnypointMQChannelBinding` | | `AnypointMQMessageBinding` |
| Google Cloud Pub/Sub ¹ | | `GooglePubSubChannelBinding` | | `GooglePubSubMessageBinding` |
| HTTP | | | `HttpOperationBinding` | `HttpMessageBinding` |
| IBM MQ ¹ | `IBMMQServerBinding` | `IBMMQChannelBinding` | | `IBMMQMessageBinding` |
| JMS ¹ | `JMSServerBinding` | `JMSChannelBinding` | | `JMSMessageBinding` |
| Kafka | `KafkaServerBinding` | `KafkaChannelBinding` | `KafkaOperationBinding` | `KafkaMessageBinding` |
| MQTT | `MQTTServerBinding` | | `MQTTOperationBinding` | `MQTTMessageBinding` |
| MQTT 5 ¹ | `MQTT5ServerBinding` | | | |
| NATS ¹ | | | `NATSOperationBinding` | |
| Pulsar | `PulsarServerBinding` | `PulsarChannelBinding` | | |
| ROS 2 ¹ | `ROS2ServerBinding` | | `ROS2OperationBinding` | |
| SNS | | `SnsChannelBinding` | `SnsOperationBinding` | |
| Solace ¹ | `SolaceServerBinding` | | `SolaceOperationBinding` | |
| SQS | | `SqsChannelBinding` | `SqsOperationBinding` | |
| WebSockets | | `WebSocketsChannelBinding` | | |

¹ Added in `ByteBard.AsyncAPI.NET.Bindings` 3.1.0-beta.19; not available in the 3.0.1 stable release. The types
live in a namespace per protocol, such as `ByteBard.AsyncAPI.Bindings.Solace` or
`ByteBard.AsyncAPI.Bindings.GooglePubSub`. A cell left empty means the package has no binding at that level
for the protocol.

Every binding implements `IServerBinding`, `IChannelBinding`, `IOperationBinding` or `IMessageBinding`, which
are the types the `Add…Binding` methods below accept, so the newer bindings work without any change on this
library's side.

SignalR, gRPC, SSE and WebRTC bindings ship in this repository's own packages; see the other pages in this
section.

## Registering bindings

Each binding level has a method on `AsyncApiOptions`. The first argument is the name the binding is
registered under in the document's `components`. Servers and channels are attached by that name, and a
channel can instead name its bindings through a `BindingsRef`; operations and messages are attached only
through a `BindingsRef` (see below).

```csharp
using Bielu.AspNetCore.AsyncApi.Extensions;
using ByteBard.AsyncAPI.Bindings.Kafka;
using ByteBard.AsyncAPI.Models;

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
| `AddChannelBinding` | `components/channelBindings` | Any `[Channel(BindingsRef = "...")]` that names it, and the channel of the same name when that channel has no `BindingsRef` |
| `AddOperationBinding` | `components/operationBindings` | Any operation attribute with a matching `BindingsRef` |
| `AddMessageBinding` | `components/messageBindings` | Any `[Message(BindingsRef = "...")]` |

```csharp
using Bielu.AspNetCore.AsyncApi.Attributes;

[AsyncApi]
public class OrderEvents
{
    [Channel("orders")]
    [Message(typeof(OrderPlaced), BindingsRef = "orderPlaced")]
    [PublishOperation(typeof(OrderPlaced), BindingsRef = "publishOrder")]
    public void PublishOrder(OrderPlaced order) { }
}
```

The `orders` channel above declares no `BindingsRef`, so it gets the bindings registered under its own name.
A channel that does declare one uses only that: a `BindingsRef` takes precedence over the channel's name.

An operation or message binding that no attribute references is added to `components` but attached to
nothing. Conversely, a `BindingsRef` that matches no registered name is ignored: the element is generated
without bindings.

Calling an `Add…Binding` method again with the same name adds a second binding under it, which is how one
channel documents more than one protocol. Two bindings of the *same* protocol under one name do not
accumulate: the last one registered replaces the earlier one, and no exception is thrown.

## Known upstream issues in 3.0.1

`ByteBard.AsyncAPI.NET.Bindings` 3.0.1 has three bugs, none in this library. Each one produces a document
that the official AsyncAPI JSON Schema rejects. **They are fixed in 3.1.0-beta.19**, so upgrading to that
version or later removes the need for a workaround:

- **WebSockets:** the binding is written under the key `websockets`; the AsyncAPI bindings specification
  uses `ws`.
- **Kafka message binding:** `SchemaIdLocation` is written as `SchemaIdLocation` instead of
  `schemaIdLocation`.
- **SQS:** `fifoQueue` is required by the schema but is omitted when `Queue.FifoQueue` is `false`, so a
  standard (non-FIFO) queue serializes without it.

If you stay on 3.0.1, a transformer registered with `AsyncApiOptions.AddSerializedDocumentTransformer` can
correct the serialized document before it is served.
