---
"bielu-aspnetcore-asyncapi": minor
---

New `dotnet arazzo impact` command lists the workflow steps an OpenAPI or AsyncAPI change affects. It resolves every step against the source documents before (`--base name=path`) and after (`--head name=path`) the change. A target that no longer resolves is a breaking change (`--fail-on-breaking` exits 1). A target whose definition changed, with local `$ref`s followed so a payload schema edit counts, is listed for review. `--format markdown` renders a pull-request comment. `Bielu.Arazzo.NET` gains `ArazzoReferenceValidator.ResolveStepTargets`, which returns what each step's target resolved to (`ArazzoStepTarget`).
