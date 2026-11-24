---
"@bielu/scalar-arazzo": minor
"bielu-aspnetcore-asyncapi": minor
---

The Arazzo Sequence view frames steps that run at the same time in a UML `par` fragment, with a dashed line between the branches: consecutive steps of a workflow that uses `dependsOn`, none of which waits on another (by `dependsOn` or by reading its outputs). Stages can be branches of a `par`, and a stage's own steps can run in parallel inside its band; nested frames are inset so every edge shows.
