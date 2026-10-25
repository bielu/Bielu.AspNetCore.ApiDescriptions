---
"bielu-aspnetcore-asyncapi": major
---

`AddAsyncApi()` and `AddAsyncApiForApiVersions()` no longer call `AddEndpointsApiExplorer()`. Document generation hasn't used ApiExplorer since the transformer contexts stopped exposing it, so registering it only added services the app might not want.

An app that relied on `AddAsyncApi()` to register the endpoint ApiExplorer for something else, such as Swashbuckle's `AddSwaggerGen()` describing minimal APIs, must now call `builder.Services.AddEndpointsApiExplorer()` itself. That includes apps that also use controllers: `AddControllers()` registers the ApiExplorer for controllers only, so without the call their minimal APIs drop out of the Swagger document. `AddOpenApi()` registers what it needs, so apps using it are unaffected. So are apps using `AddAsyncApiForApiVersions()`, since Asp.Versioning's `AddApiExplorer()`, which it requires, registers the endpoint ApiExplorer as well.
