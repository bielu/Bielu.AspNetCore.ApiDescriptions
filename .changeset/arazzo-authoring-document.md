---
"bielu-aspnetcore-asyncapi": major
---

`ArazzoOptions` can now author the rest of an Arazzo document: `WithSelf` sets `$self` (Arazzo 1.1), `WithExtension` adds root `x-*` extensions, and `AddComponentInputs`, `AddComponentParameter`, `AddComponentSuccessAction` and `AddComponentFailureAction` fill `components`. Workflows and steps refer to them with `ParameterReference`, `OnSuccessReference`, `OnFailureReference` and, for a shared inputs schema, `ArazzoWorkflowBuilder.WithInputsReference`. Both builders also gain `WithExtension`. These reach the document before validation, and `ArazzoValidator` now checks that every `$components` reference and `#/components/inputs` `$ref` resolves to an entry of the right kind, validates component parameters and actions like inline ones, and rejects a `$self` that has a fragment or appears in an Arazzo 1.0 document.

**Breaking:** documents with unresolved or mismatched `$components` references, `value` on a non-parameter reference, invalid components, or a `$self` that has a fragment or sits in a `1.0.x` document now fail validation, and with `AddArazzo` fail startup. See [Migrating to 3.0](https://apidescriptions.bielu.pl/articles/migration-to-3.0.html).
