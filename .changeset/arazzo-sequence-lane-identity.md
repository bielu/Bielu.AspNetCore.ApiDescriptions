---
"@bielu/scalar-arazzo": patch
"bielu-aspnetcore-asyncapi": patch
---

The Sequence view keeps two services apart when an expanded workflow from another Arazzo document calls its source by the same name as the parent does. A lane was keyed by the source name alone, so the parent's `api` (the shop) and the invoked document's `api` (billing) shared one lane, titled after whichever came first. Another document's source now shares a lane with the parent's only when both point at the same URL.
