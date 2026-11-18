---
"bielu-aspnetcore-asyncapi": minor
---

`ArazzoOptions.AddDocumentTransformer` registers an `IArazzoDocumentTransformer` (an instance, a type created from the application services, or a delegate) that modifies the Arazzo document's object model once it is built and before it is validated, as `AsyncApiOptions.AddDocumentTransformer` does for AsyncAPI documents. Transformers run in registration order, once per document.
