---
"@bielu/scalar-arazzo": patch
"bielu-aspnetcore-asyncapi": patch
---

A source description now finds its document when Scalar loaded that document from a different URL than the one the source names, as long as the document's `$self` (OpenAPI 3.2's canonical URI) is that URL. Behind a proxy or service discovery, for example Scalar in .NET Aspire loading `https://authservice/openapi/v1.json` while the Arazzo source names `https://localhost:7430/openapi/v1.json`, the source showed "Not loaded" and its steps did not resolve. A relative `$self` is resolved against where the document was loaded from.
