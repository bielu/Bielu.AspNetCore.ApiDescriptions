---
"bielu-aspnetcore-asyncapi": minor
---

Add `Bielu.AspNetCore.AsyncApi.Avro`, a new package that describes message payloads as Apache Avro schemas. `AsyncApiOptions.AddAvroPayloads()` replaces each message's JSON Schema payload with the equivalent Avro schema, declared as `schemaFormat: application/vnd.apache.avro;version=1.9.0`. Objects become records, nullable and optional properties become unions with `null`, and arrays, maps, string enums, `int32`/`int64` and `float`/`double` are kept. The `uuid`, `date-time`, `date` and `time` formats become logical types, and recursive types are referenced by name. `WithNamespace` sets the namespace of named types, and `ForMessages` limits which messages are converted. The JSON Schemas stay under `components/schemas`, and headers are unchanged.
