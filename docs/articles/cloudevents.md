# CloudEvents

`Bielu.AspNetCore.AsyncApi.CloudEvents` describes a document's messages as [CloudEvents 1.0](https://github.com/cloudevents/spec/blob/v1.0.2/cloudevents/spec.md)
events. CloudEvents is the CNCF's common envelope for event data: every event carries an `id`, a `source`, a
`specversion` and a `type`, in protocol headers (binary mode) or around the data (structured mode). Without this
package the AsyncAPI document describes only the data, and consumers have to learn about the envelope somewhere else.

```sh
dotnet add package Bielu.AspNetCore.AsyncApi.CloudEvents
```

```csharp
builder.Services.AddAsyncApi(options => options
    .AddServer("kafka", "broker:9092", "kafka")
    .AddCloudEvents(cloudEvents => cloudEvents
        .WithSource("/orders")
        .WithTypePrefix("com.example.orders.")));
```

`AddCloudEvents()` adds a document transformer. Document transformers run in the order they are added, so add it
after anything else that adds messages.

## Binary mode

Binary mode is the default. The data stays the payload, and each message's headers schema gains the context attributes:

| Attribute | Schema | Required |
| --- | --- | --- |
| `id` | non-empty string | yes |
| `source` | URI reference, pinned with `const` when a source is configured | yes |
| `specversion` | `const: "1.0"` | yes |
| `type` | `const` of the event type | yes |
| `dataschema` | URI, pinned when configured for the message | no |
| `subject` | string | no |
| `time` | `date-time` | no |

`datacontenttype` is left out. In binary mode every binding carries it as the protocol's own content type, which is the
message's `contentType`.

Header names depend on the CloudEvents protocol binding:

| Binding | Server protocols | `id` header |
| --- | --- | --- |
| HTTP | `http`, `https`, `ws`, `wss`, anything else | `ce-id` |
| Kafka | `kafka`, `kafka-secure` | `ce_id` |
| AMQP | `amqp`, `amqps` | `cloudEvents:id` |
| MQTT 5 | `mqtt`, `mqtts`, `secure-mqtt`, `mqtt5` | `id` (user property) |

With `ProtocolBinding = Auto`, the default, the binding is chosen per message from the protocols of the servers its
channels use. A channel without `servers` is on every server. When those protocols disagree, or there are no servers,
the binding falls back to HTTP. To choose one yourself, call `UseProtocolBinding(CloudEventsProtocolBinding.Kafka)`.

A message that already has headers, for example from `[Message(HeadersType = typeof(...))]`, keeps them: the result is
`allOf` of the existing schema and the CloudEvents attributes.

## Structured mode

```csharp
options.AddCloudEvents(cloudEvents => cloudEvents.UseMode(CloudEventsMode.Structured));
```

The payload becomes the envelope, and the message's `contentType` becomes `application/cloudevents+json`:

```json
{
  "type": "object",
  "required": ["id", "source", "specversion", "type"],
  "properties": {
    "id": { "type": "string", "minLength": 1 },
    "source": { "type": "string", "format": "uri-reference", "const": "/orders" },
    "specversion": { "type": "string", "const": "1.0" },
    "type": { "type": "string", "const": "com.example.orders.orderPlaced" },
    "datacontenttype": { "type": "string", "const": "application/json" },
    "data": { "$ref": "#/components/schemas/OrderPlaced" }
  }
}
```

`datacontenttype` is pinned to the message's original content type, or to the document's `defaultContentType`, or to
`application/json`. A payload in another schema format, such as Avro, cannot be the `data` of a JSON Schema envelope,
so such a message is left unchanged.

## Event type and source

| Setting | Effect |
| --- | --- |
| `WithTypePrefix("com.example.")` | `type` is the prefix plus the message name (`com.example.orderPlaced`). |
| `WithSource("/orders")` | `source` is pinned to `/orders` on every message. |
| `ConfigureMessage(key, m => …)` | Overrides `Type`, `Source`, `DataSchema` or `Mode` for one message, by its key under `components/messages`. |
| `MessageFilter = key => …` | Keeps only the messages it returns `true` for. |

## Examples

Message examples gain the attributes too: header values in binary mode, and an envelope around the example payload in
structured mode. They use the configured `source` (or `/`) and a placeholder `id`.

## Conformance

The test suite validates the output of both modes against the official AsyncAPI 2.6.0 and 3.1.0 JSON Schemas.
