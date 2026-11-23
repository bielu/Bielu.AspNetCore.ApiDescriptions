---
"@bielu/scalar-arazzo": minor
"bielu-aspnetcore-asyncapi": minor
---

The Arazzo Sequence view draws a step that invokes a workflow as a stage: the invoked workflow's own requests, responses and messages, in place, on a band numbered in run order and titled with the workflow's summary, instead of one arrow into a workflow lane. A workflow composed of others now reads as its stages, with every service they call in its own lane. Stages go one level deep, as the step list shows nested workflows; a workflow that is not loaded, or has no steps, is still a call into its lane.
