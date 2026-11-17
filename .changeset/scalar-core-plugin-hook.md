---
"bielu-aspnetcore-asyncapi": patch
---

Internal: the shared Scalar console core (`@bielu/scalar-core`) gains `installScalarPluginHook`, so a bundle can add any
plugin to `window.Scalar.createApiReference`, not only a console view. `installScalarHook` now delegates to it, with no
change in behavior for the SignalR, gRPC and broker consoles.
