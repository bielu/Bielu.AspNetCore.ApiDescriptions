---
"bielu-aspnetcore-asyncapi": minor
---

`ArazzoStepBuilder.Workflow(sourceName, workflowId)` targets a workflow of another Arazzo document, as `$sourceDescriptions.{sourceName}.{workflowId}`. The BARAZZO006 analyzer checks that its source description is declared, and BARAZZO007 no longer expects the workflow id to be defined locally.
