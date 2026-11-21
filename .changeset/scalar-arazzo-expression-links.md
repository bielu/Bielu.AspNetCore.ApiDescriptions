---
"@bielu/scalar-arazzo": minor
"bielu-aspnetcore-asyncapi": minor
---

Runtime Expressions in the Arazzo view in Scalar link to what they read. `$inputs.<name>`, `$steps.<id>.outputs.<name>`, `$workflows.<id>.outputs.<name>` and `$components.<kind>.<name>` go to the workflow input, step output, workflow output or component row, opening a collapsed step first and highlighting the row, wherever they appear: parameters, request bodies (including `{$inputs.token}` embedded in a string), replacements, criteria, actions, outputs and `correlationId`. An expression naming an input, step, output, workflow, component or source description the document doesn't declare is underlined in red, with the reason in its tooltip. Each step output and workflow output lists what reads it ("Used by …"), and a parameter resolved from a component says which one. The ArazzoScalar example's `status` step now declares the `sku` output its `cancelled` action reads.
