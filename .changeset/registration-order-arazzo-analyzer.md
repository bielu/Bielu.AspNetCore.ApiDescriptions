---
"bielu-aspnetcore-asyncapi": patch
---

The BASYNC010 analyzer now also warns when `AddOpenApi()` is called after `AddArazzo()` in the same method, and the build-time generation docs state exactly which registration orders affect which tools.
