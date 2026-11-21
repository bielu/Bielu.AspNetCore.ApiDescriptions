---
"bielu-aspnetcore-asyncapi": minor
---

Bielu.AspNetCore.Arazzo: add `IArazzoSourceDocumentProvider`, the seam that supplies the document behind a self-wired source, and `ArazzoOptions.AddSelfWiredSource(...)` to self-wire a source against any provider's document. The `openapi` and `asyncapi` lookups behind `AddOpenApiSource`/`AddAsyncApiSource` are now the two built-in providers; providers for one type are asked newest first, so an app can validate steps against, for example, a partner's OpenAPI description shipped with it. A self-wired source whose type has no provider is now a startup validation error instead of an `InvalidOperationException`.
