---
"@bielu/scalar-arazzo": patch
"bielu-aspnetcore-asyncapi": patch
---

The Arazzo workflow overview reorders its rows when their arrows would cross, as in the Data flow view of a workflow whose steps take values from both a step and another workflow. The order the steps are walked in still decides the layout wherever no arrow crosses, so diagrams without crossings don't change; small diagrams try every order of their rows, and bigger ones fall back to barycenter sweeps.
