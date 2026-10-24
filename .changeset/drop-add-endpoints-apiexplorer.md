---
"bielu-aspnetcore-asyncapi": major
---

`AddAsyncApi()` and `AddAsyncApiForApiVersions()` no longer call `AddEndpointsApiExplorer()`. Document generation hasn't used ApiExplorer since the transformer contexts stopped exposing it, so registering it only added services the app might not want. Versioned documents are still discovered without it, from both controllers and minimal APIs.

An app that relied on `AddAsyncApi()` to register the endpoint ApiExplorer for something else, such as Swashbuckle's `AddSwaggerGen()` describing minimal APIs, must now call `builder.Services.AddEndpointsApiExplorer()` itself. `AddOpenApi()` and `AddControllers()` register what they need, so apps using them are unaffected.
