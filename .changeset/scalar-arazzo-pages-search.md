---
"@bielu/scalar-arazzo": patch
"bielu-aspnetcore-asyncapi": patch
---

The Arazzo document's filter also searches what the Pages view shows: workflow, step and action labels, and the titles and routes of the pages steps are on and actions land on. Every box in the workflow overview opens its step, including the steps of an expanded workflow, which open in that workflow's section even when another workflow has a step with the same id. Opening a step reveals it when the filter hides it, unfolds it, scrolls to it and moves keyboard focus to it, from the inline and the full-window overview; in the classic layout the step's workflow opens first if it is closed. The filter reads the page map once per search, and does no Pages work when no Pages option is set.
