---
"bielu-aspnetcore-asyncapi": minor
---

The source generator now picks up `[Message]` placed on a payload class, so class-level message metadata applies under Native AOT and the source-generated document matches the reflection-based one. `IAsyncApiMetadataProvider` gains a default interface member, `GetPayloadMessageAttributes()`, that the generated provider implements; providers that leave it at `null` keep reading the attribute by reflection.
