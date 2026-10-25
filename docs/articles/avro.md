# Avro payloads

`Bielu.AspNetCore.AsyncApi.Avro` describes message payloads as [Apache Avro](https://avro.apache.org/docs/1.11.1/specification/)
schemas. AsyncAPI lets a payload use a schema format other than JSON Schema through `schemaFormat`. Kafka teams that
keep their schemas in a schema registry (Confluent, Apicurio, Karapace) usually expect Avro there.

```sh
dotnet add package Bielu.AspNetCore.AsyncApi.Avro
```

```csharp
builder.Services.AddAsyncApi(options => options
    .AddServer("kafka", "broker:9092", "kafka")
    .AddAvroPayloads(avro => avro.WithNamespace("com.example.orders")));
```

Each message payload becomes a multi-format schema:

```json
"payload": {
  "schemaFormat": "application/vnd.apache.avro;version=1.9.0",
  "schema": {
    "type": "record",
    "name": "OrderPlaced",
    "namespace": "com.example.orders",
    "fields": [
      { "name": "orderId", "type": { "type": "string", "logicalType": "uuid" } },
      { "name": "placedAt", "type": { "type": "long", "logicalType": "timestamp-millis" } },
      { "name": "note", "type": ["null", "string"] }
    ]
  }
}
```

In an AsyncAPI 2.6 document the `schemaFormat` sits on the message, as that version defines it.

## How JSON Schema maps to Avro

The Avro schema is converted from the JSON Schema the generator already produces, so it follows the same .NET types,
naming policy and schema transformers. There is no second definition to keep in sync.

| JSON Schema | Avro |
| --- | --- |
| object with properties | `record`, named in PascalCase after its schema or its place in the payload (`OrderPlacedLinesItem`) |
| nullable (`"type": ["null", …]`) | union with `null` first |
| property not in `required` | union with `null`, defaulting to `null` |
| `array` | `array` |
| object with only `additionalProperties`, or a free-form object | `map` (of `string` when the values are free-form) |
| string `enum` whose values are valid Avro names | `enum` |
| `oneOf` / `anyOf`, or several types | union, without duplicate branches |
| `integer` with `int32` / otherwise | `int` / `long` |
| `number` with `float` / otherwise | `float` / `double` |
| `boolean` | `boolean` |
| `string` with `uuid` / `date-time` / `date` / `time` | `uuid` / `timestamp-millis` / `date` / `time-millis` logical types |
| `string` with `byte` (`byte[]`) | `bytes` |
| any other `string` | `string` |
| `$ref` to a type already defined in the payload | its full name, so recursive types stay finite |

Avro defines each named type once per schema and refers to it by name after that. A reference that cannot be resolved
becomes `string`.

## Options

| Option | Effect |
| --- | --- |
| `WithNamespace("com.example")` | Namespace of every record and enum. |
| `ForMessages("orderPlaced", …)` / `MessageFilter` | Only these messages, by their key under `components/messages`. |
| `AvroVersion` | The version declared in `schemaFormat`. Defaults to `1.9.0`, the version the AsyncAPI specification lists. |

## What stays the same

- The JSON Schemas stay under `components/schemas`, so anything else that references them keeps working.
- Headers stay JSON Schema. Avro describes only the payload.
- A payload already in another schema format is left alone.

`AddAvroPayloads()` adds a document transformer. Document transformers run in the order they are added, so add it
after anything else that adds messages, such as `AddWolverine()`.

## Conformance

The test suite validates the output against the official AsyncAPI 2.6.0 and 3.1.0 JSON Schemas. Those schemas include
the Avro schema definition, so the Avro output is checked too.
