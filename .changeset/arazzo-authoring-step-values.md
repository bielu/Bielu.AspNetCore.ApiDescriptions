---
"bielu-aspnetcore-asyncapi": major
---

`ArazzoStepBuilder` can now author the rest of a step's values. `Replacement(target, value, targetSelectorType)` adds Payload Replacement Objects, kept whether it is called before or after `Payload`/`PayloadExpression`; `PayloadSelector` sets a Selector Object payload; `Parameter(ArazzoParameter)` takes a literal, expression or Selector Object value; and `Output(name, ArazzoSelector)` declares a selector output. `ArazzoValidator` now rejects literal outputs, and Selector Objects or `targetSelectorType` in a document that declares Arazzo 1.0, since both are new in 1.1.

**Breaking:** documents that passed validation before now fail it, and with `AddArazzo` that fails startup and `dotnet getdocument`. Affected are documents with literal outputs, and `1.0.x` documents with Selector Objects or `targetSelectorType`. See [Migrating to 3.0](https://apidescriptions.bielu.pl/articles/migration-to-3.0.html).
