---
"bielu-aspnetcore-asyncapi": minor
---

deprecate `Bielu.AspNetCore.AsyncApi.UI` in favour of Scalar: `MapAsyncApiUi()` is now `[Obsolete]` and the package description marks it deprecated. Render the generated document with `Scalar.AspNetCore`'s `MapScalarApiReference(options => options.AddAsyncApiDocument(...))` instead (the `MapAsyncApi()` document endpoint is unchanged), optionally with `Bielu.AspNetCore.AsyncApi.Scalar.SignalR` / `.Scalar.Grpc` for interactive protocol consoles. All in-repo examples (StreetlightsAPI, SignalRChat, GrpcGreeter, Aspire Mini Shop) were migrated off the built-in UI to Scalar.
