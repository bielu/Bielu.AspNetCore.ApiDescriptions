---
"bielu-aspnetcore-asyncapi": patch
---

Arazzo startup validation no longer fails on steps that target a source added by URL alone with `AddSourceDescription` (another service's OpenAPI or AsyncAPI document), which it can't fetch at startup; it checks only steps whose source is self-wired, and still reports a step naming a source the document doesn't declare. A qualified `operationId` (`$sourceDescriptions.<name>.<operationId>`) is now resolved against the source it names, so one that targets a self-wired source no longer fails startup when it exists.
