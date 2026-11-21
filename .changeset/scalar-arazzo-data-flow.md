---
"@bielu/scalar-arazzo": minor
"bielu-aspnetcore-asyncapi": minor
---

The Arazzo workflow overview in Scalar has a Data flow view (Technical | Data flow, with Pages when the document describes its pages): the workflow's inputs, its steps, other workflows whose outputs it reads and its own outputs, with an arrow from where each value comes from to every step or output that reads it, named on the arrow. A receiving step's `correlationId` now counts as reading the value it is matched on, in the step's "Used by" lists and the diagnostics as well. The choice of view is remembered in the browser.
