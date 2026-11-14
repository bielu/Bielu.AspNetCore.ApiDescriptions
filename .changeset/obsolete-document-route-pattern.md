---
"bielu-aspnetcore-asyncapi": patch
---

`AsyncApiOptions.DocumentRoutePattern` is now marked `[Obsolete]` and will be removed in the next major version.
Nothing reads it and setting it has no effect: the document is served from the pattern passed to `MapAsyncApi()`.
`MapAsyncApi()` wrote it to the unnamed options instance only, so the options of a named document always reported the
default route. Its value is unchanged in this release.
