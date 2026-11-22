---
"@bielu/scalar-arazzo": minor
"bielu-aspnetcore-asyncapi": minor
---

In the Arazzo workflow overview's Data flow view in Scalar, an arrow's label opens what flows along it, by pointer, Enter or Space. For each value the dialog shows the expression it is set from (`$response.body#/id`, or a Selector Object's context and JSONPath), where that is (the 201 response body of `POST /orders`, a message payload, the workflow's inputs), where the receiving step or the workflow's outputs use it (`query parameter order`, `correlation`), its schema as the loaded OpenAPI or AsyncAPI document describes it (type, format, bounds, nested properties), and a link to where that schema is defined: the named schema's page in Scalar, else the operation or channel, or the input's row.
