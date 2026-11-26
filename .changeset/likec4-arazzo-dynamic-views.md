---
"bielu-aspnetcore-asyncapi": minor
---

`Bielu.AspNetCore.AsyncApi.LikeC4` draws Arazzo workflows as LikeC4 dynamic views (sequence variant). `AsyncApiLikeC4Generator.Generate(sources, workflows)` takes `ArazzoLikeC4Source` documents (1.0.x or 1.1.x, JSON or YAML), and `dotnet asyncapi likec4` gets `--arazzo [name=]<path|url>`. A step on an operation or channel of a source description that is one of the AsyncAPI documents (by name, or by its URL being the document's link) is an arrow to or from that channel's element; other source descriptions become `arazzoSource` elements, and a step invoking a workflow opens that workflow's view (`navigateTo`). Steps start at the client, or at the `x-actor` the step or workflow names, titled by the document's `x-actors` registry, as in the Scalar Arazzo plugin's sequence view.
