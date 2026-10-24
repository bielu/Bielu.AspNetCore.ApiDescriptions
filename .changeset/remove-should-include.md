---
"bielu-aspnetcore-asyncapi": major
---

`AsyncApiOptions.ShouldInclude` is removed. It was a `Func<ApiDescription, bool>` carried over from `Microsoft.AspNetCore.OpenApi`, but document generation never called it, so setting it had no effect. Channels come from attributes and integrations, not from ApiExplorer descriptions. Use `AddIncludedChannel(...)` / `IncludeOnlyChannels` to limit a document to specific channels, or a document transformer for anything more selective.

`Bielu.AspNetCore.AsyncApi.Versioning` set `ShouldInclude` to filter each version's document by its ApiExplorer group. Since nothing read it, versioned documents were never filtered, and that doesn't change: each still gets the version's `info` and deprecation flag.
