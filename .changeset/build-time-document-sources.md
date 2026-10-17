---
"bielu-aspnetcore-asyncapi": minor
---

Add `Bielu.AspNetCore.ApiDescriptions`, a new package that owns build-time document generation (`dotnet getdocument`) for every document family. Packages register an `IBuildTimeDocumentSource` with `AddBuildTimeDocumentSource<TSource>()`, and one provider lists and writes their documents next to `Microsoft.AspNetCore.OpenApi`'s. OpenAPI names are settled first, then sources in ascending `Order`; a document keeps its own name unless an earlier document already uses it, in which case it gets its source's prefix. `Bielu.AspNetCore.AsyncApi` now depends on it and registers AsyncAPI as the first source, so existing AsyncAPI and OpenAPI file names are unchanged. Asking build-time generation for a document name it doesn't list now throws instead of being treated as an AsyncAPI document name.
