---
"bielu-aspnetcore-asyncapi": minor
---

Add support for embedding examples in AsyncAPI messages.
Includes the `[MessageExample]` attribute for declarative example definition on message types and the `AddMessageExample<T>()` fluent configuration on `AsyncApiOptions`.
Examples can be provided as JSON literals, via provider types, or as object instances.
Added the `SetSchemaExampleFromMessageExample` option to automatically promote the first message example to its associated JSON schema.
