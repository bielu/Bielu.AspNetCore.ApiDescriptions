---
"bielu-aspnetcore-asyncapi": minor
---

Added `AsyncApiOptions.AsyncApiJsonSchemaJsonOptions` (previously documented but missing): a per-document `JsonSerializerOptions` used only for schema generation, e.g. to document payload properties in PascalCase without changing the app's runtime JSON options.
