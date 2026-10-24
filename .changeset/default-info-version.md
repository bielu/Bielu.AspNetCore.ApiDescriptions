---
"bielu-aspnetcore-asyncapi": major
---

`AsyncApiGeneratorConstants.DefaultAsyncApiVersion` is renamed to `DefaultInfoVersion`. Its value (`"1.0.0"`) is the default `info.version`, the version of the described API, and the old name read as the AsyncAPI specification version, which is `AsyncApiOptions.AsyncApiVersion`. The public constants on `AsyncApiGeneratorConstants` now have XML docs.
