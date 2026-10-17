---
"bielu-aspnetcore-asyncapi": minor
---

The Wolverine integration now reads `[Message]` placed on message classes (title, summary, description, name, content type, headers type, bindings, correlation id, external docs, traits and tags). Explicit `WolverineAsyncApiOptions` calls such as `WithExternalDocs<T>` take precedence over the attribute, `IncludeCorrelationId()` does not override an attribute-declared correlation id, and the component key stays Wolverine's message type name (`Name` only changes the displayed `name`; `MessageId` is ignored). The attribute-to-message mapping is now shared with the core attribute pipeline.
