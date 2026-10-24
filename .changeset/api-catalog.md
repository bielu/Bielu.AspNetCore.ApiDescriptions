---
"bielu-aspnetcore-asyncapi": minor
---

Add `Bielu.AspNetCore.ApiCatalog`, a new package that serves an RFC 9727 API catalog. `MapApiCatalog()` answers `GET` and `HEAD /.well-known/api-catalog` with an RFC 9264 Linkset (`application/linkset+json` with the RFC 9727 profile) that lists every document the app serves as a `service-desc` link: those of `MapAsyncApi()`, `MapArazzo()` and `MapOpenApi()`, with their media types. `AddApiCatalog()` adds `service-doc` links (`AddServiceDoc`), other APIs or nested catalogs (`AddItem`), and the route of a non-default `MapOpenApi()` (`OpenApiRoutePattern`).

`Bielu.AspNetCore.ApiDescriptions` gains `ApiDescriptionDocumentMetadata`, which `MapAsyncApi()` and `MapArazzo()` now add to their endpoints so the catalog can find them. An endpoint of your own can carry it too.
