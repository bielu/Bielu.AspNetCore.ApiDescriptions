---
"@bielu/scalar-arazzo": minor
"bielu-aspnetcore-asyncapi": minor
---

In the Arazzo workflow overview's Data flow view in Scalar, an arrow's label opens what flows along it, by pointer, Enter or Space. For each value the dialog shows the expression it is set from (`$response.body#/id`), where that is (the 201 response body of `POST /orders`, a message payload, the workflow's inputs), its schema as the loaded OpenAPI or AsyncAPI document describes it (type, format, bounds, nested properties), and where the receiving step or the workflow's outputs use it (`query parameter order`, `correlation`).
