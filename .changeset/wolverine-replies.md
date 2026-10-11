---
"bielu-aspnetcore-asyncapi": minor
---

Add `IncludeReplies()` to the Wolverine integration. When enabled, a `receive` operation whose handler returns a message documents it as the operation's `reply`, addressed by the `reply-uri` header, on an address-less reply channel (AsyncAPI 3.x).
