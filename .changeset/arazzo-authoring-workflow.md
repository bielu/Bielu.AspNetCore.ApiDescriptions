---
"bielu-aspnetcore-asyncapi": minor
---

`ArazzoWorkflowBuilder` can now author a workflow's `outputs` (`Output(name, expression)` and `Output(name, ArazzoSelector)`), so later workflows can read them as `$workflows.{id}.outputs.{name}`, along with its workflow-wide `parameters` (`Parameter(name, expression, location)` and `Parameter(ArazzoParameter)`), `successActions` (`OnSuccess`) and `failureActions` (`OnFailure`). Output names are checked against the spec's `^[a-zA-Z0-9.\-_]+$` key pattern.
