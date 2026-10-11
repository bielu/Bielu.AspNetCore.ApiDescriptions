---
"bielu-aspnetcore-asyncapi": minor
---

Add `AsyncApiDocument.IsAsyncApiVersion(AsyncApiVersion)`, a public extension that tells whether a document declares a given AsyncAPI major version, so transformers no longer need to inspect the `asyncapi` string themselves.
