---
"@bielu/scalar-arazzo": minor
"bielu-aspnetcore-asyncapi": minor
---

The Arazzo view in Scalar lists problems it can find against the loaded OpenAPI and AsyncAPI documents, under the document's header ("1 error · 3 warnings"), at the top of each step, beside its heading and on its box in the overview. Errors: a target the loaded sources don't declare, a source description, workflow or step that doesn't exist (in a target, `goto`, `dependsOn` or Runtime Expression), a required path parameter that isn't passed, a duplicate `stepId`. Warnings: a parameter the operation doesn't declare, a required query parameter that isn't passed, a status the criteria check that the operation doesn't return, a step depending on one whose success always ends the workflow, and an expression reading a step that doesn't run first or a step sending its own output.
