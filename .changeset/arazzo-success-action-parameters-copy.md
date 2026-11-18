---
"bielu-aspnetcore-asyncapi": patch
---

Arazzo documents built by `AddArazzo` keep the `parameters` of success actions, inline or under `components.successActions`. Each build copies the configured model, and that copy had dropped them, so they were missing from the served and build-time documents.
