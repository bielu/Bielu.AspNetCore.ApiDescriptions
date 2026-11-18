---
"bielu-aspnetcore-asyncapi": minor
---

The Aspire Scalar packages (`Bielu.AspNetCore.Arazzo.Scalar.Aspire`, `Bielu.AspNetCore.AsyncApi.Scalar.SignalR.Aspire`, `Bielu.AspNetCore.AsyncApi.Scalar.Grpc.Aspire`) now depend on Scalar.Aspire 1.0.2. Its container image bundles `@scalar/api-reference` 1.81.0; the 0.15.0 image shipped 1.78.1, older than the 1.80.0 the Arazzo plugin needs, so Arazzo documents rendered as empty OpenAPI documents in the Aspire Scalar resource. A new container test checks the whole path: `WithArazzoOptions` writes the options into the module URL, the container loads that module, and the Pages view shows the configured page and labels.
