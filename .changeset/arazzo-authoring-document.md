---
"bielu-aspnetcore-asyncapi": minor
---

`ArazzoOptions` can now author the rest of an Arazzo document: `WithSelf` sets `$self` (Arazzo 1.1), `WithExtension` adds root `x-*` extensions, and `AddComponentInputs`, `AddComponentParameter`, `AddComponentSuccessAction` and `AddComponentFailureAction` fill `components`. Workflows and steps refer to them with `ParameterReference`, `OnSuccessReference`, `OnFailureReference` and, for a shared inputs schema, `ArazzoWorkflowBuilder.WithInputsReference`. Both builders also gain `WithExtension`. These reach the document before validation, and `ArazzoValidator` now checks that every `$components` reference and `#/components/inputs` `$ref` resolves to an entry of the right kind, validates component parameters and actions like inline ones, and rejects a `$self` that has a fragment or appears in an Arazzo 1.0 document.
