---
"bielu-aspnetcore-asyncapi": patch
---

In the Arazzo view in Scalar, a step's resolved target is now a link that opens the OpenAPI operation, AsyncAPI operation or channel in its own document (Back and Forward switch documents too), or scrolls to the workflow a step invokes. Targets are named by their `operationId` or AsyncAPI name instead of the raw `{$sourceDescriptions…}` reference.
