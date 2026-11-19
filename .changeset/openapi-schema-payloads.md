---
"bielu-aspnetcore-asyncapi": minor
---

Add `Bielu.AspNetCore.AsyncApi.OpenApiSchema`, a new package that describes message payloads as OpenAPI 3.0 Schema Objects. `AsyncApiOptions.AddOpenApiSchemaPayloads()` replaces the JSON Schema payload of every message with `schemaFormat: application/vnd.oai.openapi;version=3.0.0`, converted from the generated JSON Schema: a type that includes `null` becomes `nullable: true`, several types become an `anyOf`, numeric `exclusiveMinimum`/`exclusiveMaximum` become a bound with a boolean flag, `const` becomes a one-value `enum`, `examples` becomes `example`, and the `discriminator` property name becomes a Discriminator Object. Keywords OpenAPI 3.0 lacks are dropped. Referenced component schemas are written inline, and a recursive reference stays a `$ref` to the JSON Schema component. Headers and payloads already in another schema format are left alone. `ForMessages` limits which messages are converted.
