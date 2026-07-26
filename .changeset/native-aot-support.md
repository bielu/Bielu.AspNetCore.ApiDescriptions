---
"bielu-aspnetcore-asyncapi": minor
---

Added support for Native AOT (Ahead-of-Time) compilation. Includes a new Roslyn-based Source Generator (`Bielu.AspNetCore.AsyncApi.SourceGenerators`) that discovers `[AsyncApi]`/`[Channel]` attributes at compile time and generates metadata to avoid reflection at runtime, plus a parameterless `AddAsyncApiGeneratedMetadata()` overload for the default `v1` document. See the new [Native AOT Support](https://asyncapi.bielu.pl/articles/native-aot.html) guide for setup instructions, including `JsonSerializerContext` configuration.
