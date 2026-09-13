---
"bielu-aspnetcore-asyncapi": patch
---

Registered operation transformers (`AddOperationTransformer`, delegate/instance/type-based) are now actually invoked while generating operations from `[AsyncApi]` attribute metadata. Previously they were activated and disposed but never executed. `AsyncApiOperationTransformerContext.Description` is now nullable, since operations discovered from attribute metadata have no corresponding `ApiDescription`.
