# OpenAPI schema payloads

`Bielu.AspNetCore.AsyncApi.OpenApiSchema` describes message payloads as
[OpenAPI 3.0 Schema Objects](https://spec.openapis.org/oas/v3.0.3.html#schema-object). AsyncAPI lets a payload use a
schema format other than its own through `schemaFormat`, and the OpenAPI 3.0 Schema Object is one the specification
lists. Use it when the tools that read your event payloads were built for OpenAPI, such as code generators, mock
servers or validators, so they read event payloads the same way they read HTTP request and response bodies.

```sh
dotnet add package Bielu.AspNetCore.AsyncApi.OpenApiSchema
```

```csharp
builder.Services.AddAsyncApi(options => options
    .AddServer("kafka", "broker:9092", "kafka")
    .AddOpenApiSchemaPayloads());
```

Each message payload becomes a multi-format schema:

```json
"payload": {
  "schemaFormat": "application/vnd.oai.openapi;version=3.0.0",
  "schema": {
    "type": "object",
    "properties": {
      "orderId": { "type": "string", "format": "uuid" },
      "total": { "type": "number", "format": "double", "minimum": 0, "exclusiveMinimum": true },
      "note": { "type": "string", "nullable": true }
    }
  }
}
```

In an AsyncAPI 2.6 document the `schemaFormat` sits on the message, as that version defines it.

## How JSON Schema maps to OpenAPI 3.0

The OpenAPI schema is converted from the JSON Schema the generator already produces, so it follows the same .NET types,
naming policy and schema transformers. There is no second definition to keep in sync. OpenAPI 3.0's Schema Object is a
dialect of JSON Schema, so most keywords carry over unchanged. These are rewritten:

| JSON Schema | OpenAPI 3.0 |
| --- | --- |
| `"type": ["null", "string"]` | `"type": "string", "nullable": true` |
| several types, `"type": ["string", "integer"]` | `anyOf` with one `type` per branch |
| `"type": "null"` | `"nullable": true, "enum": [null]` |
| a `null` branch in `oneOf` / `anyOf` | dropped, with `"nullable": true` |
| `"exclusiveMinimum": 0` | `"minimum": 0, "exclusiveMinimum": true` (likewise for the maximum) |
| `const` | a one-value `enum` |
| `examples` | `example`, the first of them |
| `"discriminator": "kind"` | `"discriminator": { "propertyName": "kind" }` |
| `false` schema | `"not": {}`, or `false` as `additionalProperties` |
| `$ref` to `components/schemas` | the referenced schema, written inline |

OpenAPI 3.0 allows no keywords beyond its own, so `if`/`then`/`else`, `contains`, `patternProperties`,
`propertyNames` and `additionalItems` are dropped. Extensions (`x-…`) are kept.

### References

Referenced component schemas are written inline, so the whole payload is in the OpenAPI dialect. A recursive type
cannot be inlined forever: the reference that would repeat a schema already being written stays a `$ref` to the JSON
Schema under `components/schemas`. A reference outside `components/schemas`, or one that cannot be resolved, is kept
as it is.

## Options

| Option | Effect |
| --- | --- |
| `ForMessages("orderPlaced", …)` / `MessageFilter` | Only these messages, by their key under `components/messages`. |

The declared format is always `application/vnd.oai.openapi;version=3.0.0`, the only OpenAPI version the AsyncAPI
specification lists.

## What stays the same

- The JSON Schemas stay under `components/schemas`, so anything else that references them keeps working.
- Headers stay JSON Schema.
- A payload already in another schema format, such as Avro or Protobuf, is left alone.

`AddOpenApiSchemaPayloads()` adds a document transformer. Document transformers run in the order they are added, so add
it after anything else that adds messages, such as `AddWolverine()`.

## Conformance

The test suite validates the output against the official AsyncAPI 2.6.0 and 3.1.0 JSON Schemas. Those schemas include
the OpenAPI 3.0 Schema Object definition, so the converted payloads are checked too.
