---
"@bielu/scalar-arazzo": minor
"bielu-aspnetcore-asyncapi": minor
---

The Arazzo workflow overview expands calls into another Arazzo document. A step or action that invokes `$sourceDescriptions.<name>.<workflowId>` resolves that workflow in the Arazzo document the source names, when Scalar has loaded it, even if the calling document has a workflow with the same id. Its steps are drawn in its group and resolve in their own document: component actions, the Pages view's page registry, and step targets. The cycle check is keyed by document and workflow, so a call back into the calling document is marked as a cycle. When the document isn't loaded, the node keeps a readable `identity.signIn` label. The step details resolve the same workflow: its summary, the "Show its steps" list, and a link to it in its own document.
