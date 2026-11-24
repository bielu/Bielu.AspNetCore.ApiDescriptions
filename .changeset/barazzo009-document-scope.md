---
"bielu-aspnetcore-asyncapi": patch
---

BARAZZO009 now checks a `Channel` step against the AsyncAPI document its source is bound to, using the channels runtime discovery puts there. The source name is resolved in the step's own Arazzo document, so a name that one document self-wires and another points at a partner's URL no longer warns on the partner's valid channels. A `[Channel]` on a type without `[AsyncApi]`, on a non-public method, or on an `[AsyncApi("v2")]` type for a source bound to `v1` no longer satisfies the check, because none of them reaches that document. The message now names the AsyncAPI document.
