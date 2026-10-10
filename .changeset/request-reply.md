---
"bielu-aspnetcore-asyncapi": minor
---

Add explicit request/reply declaration to operation attributes: `ReplyChannel`, `ReplyAddressLocation`, `ReplyAddressDescription` and `ReplyMessageIds` on `[PublishOperation]`/`[SubscribeOperation]` write the operation's `reply` object (AsyncAPI 3.x).
