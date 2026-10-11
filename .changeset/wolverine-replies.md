---
"bielu-aspnetcore-asyncapi": minor
---

Document Wolverine handler responses as operation replies. A `receive` operation whose handler returns a message that has no route now gets a `reply`, addressed by the `reply-uri` header, on an address-less reply channel (AsyncAPI 3.x). `IncludeReplies()` extends this to returned messages that also have a route.
