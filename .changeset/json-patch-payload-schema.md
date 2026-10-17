---
"bielu-aspnetcore-asyncapi": patch
---

Fix document generation failing for any message payload with a JSON Patch property (`JsonPatchDocument` or `JsonPatchDocument<T>`). The RFC 6902 operation schemas now close their objects with the equivalent `"additionalProperties": { "not": {} }` instead of the boolean `false`, which could not be read back into the schema model. Unused internal code left over from the `Microsoft.AspNetCore.OpenApi` port is also removed.
