---
"bielu-aspnetcore-asyncapi": major
---

**Breaking:** Arazzo source resolvers are typed. `IArazzoSourceResolver` gains `DocumentType`, and the new `ArazzoSourceResolver<TDocument>` base class takes the document as its own type, checks it once, and throws a clear `ArgumentException` for a document of the wrong type instead of resolving nothing. `AsyncApiSourceResolver` and `OpenApiSourceResolver` derive from it, so their methods take an `AsyncApiDocument` or an `OpenApiDocument` rather than `object`.
