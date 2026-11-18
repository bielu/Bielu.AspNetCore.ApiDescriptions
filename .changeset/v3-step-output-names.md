---
"bielu-aspnetcore-asyncapi": major
---

**Breaking:** output names must match the spec's `^[a-zA-Z0-9.\-_]+$` key pattern. `ArazzoStepBuilder.Output` now throws `ArgumentException` for a name outside it, as `ArazzoWorkflowBuilder.Output` already did, and `ArazzoValidator` reports such names in documents it reads.
