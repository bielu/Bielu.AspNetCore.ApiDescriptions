---
"bielu-aspnetcore-asyncapi": major
---

**Breaking:** `AsyncApiOptions.DocumentRoutePattern`, obsolete since 2.0, is removed. Nothing read it; a document's route is the pattern passed to `MapAsyncApi(pattern)`.
