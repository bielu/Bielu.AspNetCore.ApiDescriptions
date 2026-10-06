---
"bielu-aspnetcore-asyncapi": patch
---

`float`, `double` and `decimal` payload properties are now always documented as `type: number` with their format, even when System.Text.Json reports a string wire type (e.g. `[JsonNumberHandling(JsonNumberHandling.WriteAsString)]`). The leftover numeric-string `pattern` is dropped along with the `string` type. Nullable variants keep `null`.
