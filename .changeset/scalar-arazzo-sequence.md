---
"@bielu/scalar-arazzo": minor
"bielu-aspnetcore-asyncapi": minor
---

The Arazzo workflow overview in Scalar has a Sequence view: a lane for the client running the workflow, one per source description it talks to and one per workflow it invokes, with each step's request and expected response, the messages it publishes or waits for (with timeout and correlation) and its workflow calls, top to bottom in run order. Failure paths are noted under each step's arrows, and a step's name opens the step.
