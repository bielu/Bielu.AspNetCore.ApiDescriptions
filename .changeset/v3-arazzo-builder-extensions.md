---
"bielu-aspnetcore-asyncapi": minor
---

The Arazzo builders can now add `x-*` Specification Extensions everywhere the specification allows them: `ArazzoOptions.WithSourceDescriptionExtension` and `WithComponentsExtension`, `ArazzoStepBuilder.WithRequestBodyExtension`, and the new model-object overloads `Replacement(ArazzoPayloadReplacement)` and `SuccessCriteria(ArazzoCriterion)`, which also carry their own extensions.
