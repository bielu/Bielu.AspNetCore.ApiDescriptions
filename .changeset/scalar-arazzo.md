---
"bielu-aspnetcore-asyncapi": minor
---

Add `Bielu.AspNetCore.Arazzo.Scalar`, which renders Arazzo documents in the Scalar API Reference: `AddArazzoDocument(...)` adds a `MapArazzo()` document as a Scalar source, `WithArazzo()` loads the plugin, and `MapScalarArazzoAssets()` serves it. Workflows and steps get their own sidebar entries, and each step shows the OpenAPI operation or AsyncAPI channel it calls, resolved in the documents loaded next to it. Needs a Scalar version with plugin document types (scalar/scalar#10816).
