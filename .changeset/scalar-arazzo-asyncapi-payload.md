---
"bielu-aspnetcore-asyncapi": patch
"@bielu/scalar-arazzo": patch
---

In the Arazzo view in Scalar, an AsyncAPI step now shows the message it sends or receives: the payload as a property list (references followed into the AsyncAPI document), its content type and its first example payload. Payloads in other schema formats, such as Avro, are shown as written.
