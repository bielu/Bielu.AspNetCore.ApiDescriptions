---
"bielu-aspnetcore-asyncapi": minor
---

Added Roslyn analyzers to provide compile-time diagnostics for common AsyncAPI attribute misuses (missing [AsyncApi], operations without channels, duplicate names, unused document names, invalid payload types, invalid JSON in examples, and missing parameterless constructors for example providers). The analyzers also verify ID naming conventions and ensure basic documentation is present. The analyzers are bundled into the Bielu.AspNetCore.AsyncApi.Attributes package.
