---
"@bielu/scalar-arazzo": minor
"bielu-aspnetcore-asyncapi": minor
---

An OpenAPI step in the Arazzo view in Scalar shows what its operation accepts and returns, as AsyncAPI steps already show their message: the request body schema (in the media type the step sends, the others named) and each response by status, with its schema, example and headers. The response the step's success criteria check for (`$statusCode == 201`) is marked "Success criteria", and responses an action's criteria check are marked with the action's name. Swagger 2.0 body parameters and response schemas are read as well. The ArazzoScalar example's order endpoints now declare the 201, 400, 404 and 410 responses its workflows check for.
