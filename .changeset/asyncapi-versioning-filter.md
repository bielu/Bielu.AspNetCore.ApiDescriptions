---
"bielu-aspnetcore-asyncapi": minor
---

`AddAsyncApiForApiVersions()` now filters each version's AsyncAPI document by API version; until now every document listed every channel. A versioned controller action or minimal API handler follows the `ApiVersionMetadata` Asp.Versioning gives its endpoint, versioning conventions included. Any other `[AsyncApi]` class or method follows its `[ApiVersion]`, `[MapToApiVersion]` and `[ApiVersionNeutral]` attributes, a method's own winning over its class's. Version-neutral and unversioned declarations stay in every document. Declarations are filtered before channels, messages, schemas and operations are built, so a channel name shared across versions only carries each version's own messages and operations. This works with the reflection-based and source-generated metadata providers, and `[AsyncApi("name")]` and `AddIncludedChannel` still apply on top.
