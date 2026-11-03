---
"bielu-aspnetcore-asyncapi": patch
---

Keep the description and default of enum and complex-typed properties. A property whose type gets a schema reference id (an enum, or a named class or record, nullable or not) had its `[Description]` or XML-doc text written to an `x-ref-description` annotation that was never read back, and its `[DefaultValue]` written to `$ref`. Property schemas are always inline, so both now go on the schema's own `description` and `default`, as they already did for primitive properties.
