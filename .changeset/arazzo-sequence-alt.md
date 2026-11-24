---
"@bielu/scalar-arazzo": minor
"bielu-aspnetcore-asyncapi": minor
---

The Arazzo Sequence view draws the ways a step can end as a UML `alt` fragment instead of noting its failure paths under the arrow: the success path first (`[201]` and its response), then each conditional success action and each reachable failure action under its condition (`[400]`, `[if …]`, `[otherwise]`), with the response or message it ends on and where it leads (`rejected: end`, `→ create`, `retry after 2 s × 3`). The request stays above the `alt`, since every way starts with it.
