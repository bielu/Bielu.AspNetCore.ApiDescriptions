---
"bielu-aspnetcore-asyncapi": patch
---

Fix: recursive payload types are now described completely. Where a type recurses into itself (a tree node's
`Children`, a category's `Parent`, a person's pet whose `Owner` is a person), the schema now holds a `$ref` to that type's
component, and the type is added to `components/schemas`. It used to be inlined one level and then written as an empty
schema (`"items": {}`), so anything below the first level was untyped. The type's first occurrence stays inline, so
that property keeps its own description, default and nullability. Types whose `CreateSchemaReferenceId` returns
`null` have no component to point to, so their recursion still ends in an empty schema, now one level earlier.
