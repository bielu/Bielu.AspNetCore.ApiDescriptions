---
"@bielu/scalar-arazzo": patch
"bielu-aspnetcore-asyncapi": patch
---

The Arazzo view's badges (a step's dependencies, timeout and correlation ID, response handling, criterion types, action summaries) are Scalar's own `Badge` from `@scalar/blocks`, the pill Scalar's API reference uses. A response the step's success criteria check is marked in solid green, the way Scalar colours an OpenAPI operation's `x-badges`, instead of a green border.
