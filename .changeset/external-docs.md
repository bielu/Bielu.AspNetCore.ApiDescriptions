---
"bielu-aspnetcore-asyncapi": minor
---

Add `ExternalDocsUrl` and `ExternalDocsDescription` to `[Channel]`, `[PublishOperation]`/`[SubscribeOperation]` and `[Message]`. Setting a URL writes `externalDocs` on the generated channel, operation or message, including through the source generator.
