---
"bielu-aspnetcore-asyncapi": minor
---

`dotnet arazzo validate` can check every step's `operationId`, `operationPath` and `channelPath` against the OpenAPI/AsyncAPI files it targets. `--source <name>=<path>` names a file per source description, and `--resolve-local-sources` loads the sources whose `url` is a relative file path. This is the check `ValidateSourceReferencesOnStartup` runs in a live app, now available in CI without starting it. `Bielu.Arazzo.NET` gains the pieces behind it: `ArazzoReferenceValidator`, the file-based `OpenApiJsonSourceResolver` and `AsyncApiJsonSourceResolver`, and `ArazzoWorkspace.SourceNames`. Startup validation now runs the same validator, and its messages are unchanged.
