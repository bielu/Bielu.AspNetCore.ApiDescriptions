---
"bielu-aspnetcore-asyncapi": patch
---

Nullable payload properties keep `null` in their schema type instead of collapsing to the non-null type (e.g. `string?` is documented as `["null", "string"]`), including nullable nested object properties.
