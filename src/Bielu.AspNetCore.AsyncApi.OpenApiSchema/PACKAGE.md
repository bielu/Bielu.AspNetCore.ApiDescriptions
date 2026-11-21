# Bielu.AspNetCore.AsyncApi.OpenApiSchema

Describes your AsyncAPI message payloads as [OpenAPI 3.0 Schema Objects](https://spec.openapis.org/oas/v3.0.3.html#schema-object),
declared with `schemaFormat: application/vnd.oai.openapi;version=3.0.0`, so tools built for OpenAPI read event
payloads the way they read HTTP bodies. The OpenAPI schema is converted from the JSON Schema the generator already
produces, so it follows your .NET types without a second definition to keep in sync.

## Installation

```sh
dotnet add package Bielu.AspNetCore.AsyncApi.OpenApiSchema
```

## Usage

```csharp
builder.Services.AddAsyncApi(options => options
    .AddServer("kafka", "broker:9092", "kafka")
    .AddOpenApiSchemaPayloads(openApi => openApi
        .ForMessages("orderPlaced")));           // optional: only these messages
```

| JSON Schema | OpenAPI 3.0 |
| --- | --- |
| `"type": ["null", "string"]` | `"type": "string", "nullable": true` |
| several types | `anyOf` with one `type` per branch |
| numeric `exclusiveMinimum` / `exclusiveMaximum` | the bound with `exclusiveMinimum: true` / `exclusiveMaximum: true` |
| `const`, `examples` | a one-value `enum`, `example` |
| `discriminator` property name | Discriminator Object |
| `$ref` to `components/schemas` | written inline (a recursive one points at an OpenAPI copy, `<name>.openapi`) |

The JSON Schema stays under `components/schemas`, and headers stay JSON Schema. The output validates against
the official AsyncAPI 2.6.0 and 3.1.0 schemas, including their OpenAPI 3.0 Schema Object definition.

## Documentation

- [OpenAPI schema payloads](https://apidescriptions.bielu.pl/articles/openapi-schema.html)
- [Full documentation](https://apidescriptions.bielu.pl/)

## Feedback & Contributing

Copyright (c) 2025 Arkadiusz Biel. Released under the [MIT license](https://licenses.nuget.org/MIT).
Bug reports and contributions are welcome at [the GitHub repository](https://github.com/bielu/Bielu.AspNetCore.ApiDescriptions).
