---
"bielu-aspnetcore-asyncapi": major
---

**Breaking:** `[PublishOperation]` and `[SubscribeOperation]`, obsolete since 2.0, are removed. Use `[ReceiveOperation]` for `[PublishOperation]` and `[SendOperation]` for `[SubscribeOperation]`; they produce the same operations.
