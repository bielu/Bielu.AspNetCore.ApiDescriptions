---
"bielu-aspnetcore-asyncapi": patch
---

Let other document families join build-time generation through an internal `IBuildTimeDocumentSource`, so one `dotnet getdocument` run can also write them. A source document keeps its own name unless an AsyncAPI, OpenAPI or other document already uses it, in which case it gets the source's prefix. Existing AsyncAPI and OpenAPI file names are unchanged.
