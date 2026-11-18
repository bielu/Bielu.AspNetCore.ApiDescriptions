---
"bielu-aspnetcore-asyncapi": major
---

**Breaking:** `ArazzoValidator` now rejects every Arazzo 1.1 addition in a document declaring `arazzo: 1.0.x`. On top of `$self`, Selector Objects and `targetSelectorType`, that covers `asyncapi` source descriptions, the `channelPath`, `action`, `correlationId`, `timeout` and step `dependsOn` fields, the `querystring` parameter location, the `jsonpointer` criterion type and the `rfc9535` JSONPath version, as checked against the 1.0.1 specification. Such documents now fail `dotnet arazzo validate`, and with `AddArazzo` (`ArazzoOptions.Version = ArazzoVersion.V1_0`) they fail startup.
