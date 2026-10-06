---
"bielu-aspnetcore-asyncapi": minor
---

`[Message]` now honors `HeadersType`: the headers type gets a schema under `components/schemas` and is referenced from the message's `headers`. Added `MessageAttribute.ContentType` to set a message's `contentType` (falls back to the document's `defaultContentType` when unset).
