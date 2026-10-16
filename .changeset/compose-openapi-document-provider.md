---
"bielu-aspnetcore-asyncapi": minor
---

Fix build-time generation (`dotnet getdocument`, `OpenApiGenerateDocuments=true`) in apps that call both `AddOpenApi()` and `AddAsyncApi()`. Previously only the AsyncAPI document was written, to `<Project>.json`, and no OpenAPI document was written. Both are now written. An AsyncAPI document whose name matches an OpenAPI one (both default to `v1`) is written as `<Project>_asyncapi-<name>.json`. Set the new `AsyncApiOptions.BuildTimeDocumentName` to choose a different name.

Add the **BASYNC010** analyzer warning, reported when `AddOpenApi()` is called after `AddAsyncApi()` in the same method. Register OpenAPI first so build-time generation reliably writes both documents.

The BASYNC analyzers now reach apps that reference only `Bielu.AspNetCore.AsyncApi`. Previously they ran only when `Bielu.AspNetCore.AsyncApi.Attributes` was referenced directly, so existing BASYNC warnings may appear for the first time after upgrading.
