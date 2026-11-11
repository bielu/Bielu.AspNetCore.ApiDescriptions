---
"bielu-aspnetcore-asyncapi": minor
---

Support C# 15 union types on .NET 11. A `union` (or a `[Union]`-pattern type) used as a message payload
or a payload property is described as an `anyOf` with one entry per case, matching how
System.Text.Json serializes it (the active case, no wrapper) and how ASP.NET Core 11's OpenAPI
generator describes it. Schema transformers now run on each union case with that case's
`JsonTypeInfo`, as they already did for polymorphic derived types, and example providers may
return union values. New article: [Union types](https://apidescriptions.bielu.pl/articles/union-types.html).
