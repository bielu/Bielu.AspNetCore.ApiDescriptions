---
"bielu-aspnetcore-asyncapi": minor
---

Add operation and message traits. Register them with `AsyncApiOptions.AddOperationTrait(...)`/`AddMessageTrait(...)` and apply them with `Traits` on `[PublishOperation]`/`[SubscribeOperation]` and `[Message]`, which writes `traits` references on the generated operation or message.
