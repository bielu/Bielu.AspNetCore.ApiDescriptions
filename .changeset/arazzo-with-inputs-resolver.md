---
"bielu-aspnetcore-asyncapi": patch
---

`ArazzoWorkflowBuilder.WithInputs<TInputs>(options)` no longer throws at startup when the options have no
`TypeInfoResolver`, as with `new JsonSerializerOptions(JsonSerializerDefaults.Web)`. It now uses a copy of them with the
default reflection-based resolver, so a naming policy such as camelCase applies to the generated `inputs` schema. The
options passed in are left unchanged.
