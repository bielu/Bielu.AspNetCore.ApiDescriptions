---
"@bielu/scalar-arazzo": minor
"bielu-aspnetcore-asyncapi": minor
---

The Arazzo workflow overview in Scalar has a Sequence view: a lane for the client running the workflow, one per source description it talks to and one per workflow it invokes, with each step's request and expected response, the messages it publishes or waits for (with timeout and correlation) and its workflow calls, top to bottom in run order. Only the steps a successful run reaches are drawn: an unconditional `end` stops it and a `goto` jumps, and the run order is the one the diagnostics check against, with the steps whose outputs a step reads counted as its prerequisites. Failure paths are noted under each step's arrows, and a step's name opens the step (by pointer, Enter or Space, closing the full-window view and moving focus to the step). On a narrow screen the diagram scrolls sideways rather than shrinking its labels below a readable size.
