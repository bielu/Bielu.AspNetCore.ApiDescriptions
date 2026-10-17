---
"bielu-aspnetcore-asyncapi": minor
---

Generate Arazzo documents at build time. `dotnet getdocument` and `dotnet asyncapi getdocument` now also write the app's `AddArazzo` documents, with the version from `ArazzoOptions.Version` and serialized-document transformers (such as overlays) applied. An Arazzo document is listed under its own name, or under an `arazzo-` prefix when an AsyncAPI or OpenAPI document already has that name (`MyApi_arazzo-v1.json`); set `ArazzoOptions.BuildTimeDocumentName` to choose another name. `AddArazzo` works with or without `AddAsyncApi`, in any order.
