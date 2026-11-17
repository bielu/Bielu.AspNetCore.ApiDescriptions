---
"bielu-aspnetcore-asyncapi": minor
---

The Scalar packages now depend on `Scalar.AspNetCore` 3.0.1 (was 2.16.17), which bundles `@scalar/api-reference` 1.80.0.
That version adds the plugin `documentTypes` API, which renders non-OpenAPI documents such as Arazzo in the API
Reference. Scalar 3.0 only drops the `net8.0` and `net9.0` targets, which these packages never supported, so no public
API changes. Apps that reference `Scalar.AspNetCore` 2.x directly must move to 3.0.1 or later.
