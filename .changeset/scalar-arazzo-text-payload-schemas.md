---
"@bielu/scalar-arazzo": patch
"bielu-aspnetcore-asyncapi": patch
---

An AsyncAPI step in the Arazzo view in Scalar shows a message payload whose schema is written as text, such as the `.proto` source `Bielu.AspNetCore.AsyncApi.Protobuf` produces, as its format and source in a code block. The step used to show only the message name. JSON Schema payloads are still listed as properties.
