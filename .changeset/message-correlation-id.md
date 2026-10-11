---
"bielu-aspnetcore-asyncapi": minor
---

Add `MessageAttribute.CorrelationIdLocation` and `CorrelationIdDescription`. Setting a location (for example `$message.header#/correlationId`) writes a `correlationId` on the generated message, including through the source generator.
