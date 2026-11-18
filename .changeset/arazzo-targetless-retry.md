---
"bielu-aspnetcore-asyncapi": patch
---

`ArazzoValidator` accepts a `retry` failure action with neither `stepId` nor `workflowId`, which retries the current step as the Arazzo Specification describes. A `goto` failure action still needs a target, and an action that sets both is still rejected.
