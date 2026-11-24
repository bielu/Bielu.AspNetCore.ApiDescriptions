---
"@bielu/scalar-arazzo": patch
"bielu-aspnetcore-asyncapi": patch
---

The Sequence view no longer hides how a step that invokes a workflow can end once that workflow is expanded. A conditional `onSuccess` action such as `alreadyPaid`, or a failure action, was framed in an `alt` on the call while the invoked workflow's document was loading, and disappeared when the stage replaced the call. The stage's `alt` is now drawn right after its band, as the workflow returning to the invoking step's lane, with the same operands in both states.
