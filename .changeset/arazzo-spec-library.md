---
"bielu-aspnetcore-asyncapi": minor
---

Added `Bielu.Arazzo.NET` and `Bielu.Arazzo.NET.Readers`, a framework-free object model, JSON/YAML writers, validation, and readers for the [Arazzo Specification](https://spec.openapis.org/arazzo/latest.html) (OpenAPI workflows). Arazzo 1.1.0 added `AsyncAPI` as a first-class `sourceDescriptions` type, letting a workflow describe steps against event channels (`channelPath`, `action: send|receive`, `correlationId`) alongside HTTP operations — this is the first PR of a multi-PR effort to bring Arazzo workflow support to the suite; see `ARAZZO-PROPOSAL.md` for the full plan. Includes a runtime-expression parser/evaluator for the spec's `$inputs`/`$outputs`/`$steps`/`$workflows`/`$sourceDescriptions`/`$components` expressions.
