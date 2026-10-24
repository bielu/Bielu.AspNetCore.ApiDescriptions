---
"bielu-aspnetcore-asyncapi": minor
---

Add `Bielu.AspNetCore.AsyncApi.XRegistry`, a new package that serves the messages and payload schemas of an app's AsyncAPI documents as a read-only CNCF xRegistry (1.0-rc4). `MapXRegistry()` exposes each AsyncAPI document as a message group and a schema group. Every message is a message definition that points at its payload schema with `dataschemaformat`, `dataschemauri` and `dataschemaxid`. Schemas are served as standalone JSON Schema (draft-07), Avro or Protobuf documents with `xRegistry-` headers, and `$details` returns their metadata. The registry answers the standard read API: the registry, `/capabilities`, `/model`, groups, resources, `meta` and Versions, with the `?inline` flag and RFC 9457 errors. It also serves host-based discovery at `/.well-known/xregistry`. `AddXRegistry()` sets the registry id and name and limits which documents are included.
