---
"@bielu/scalar-arazzo": patch
"bielu-aspnetcore-asyncapi": patch
---

The Arazzo step's styles are written as Tailwind utilities in its template, the way Scalar writes its own components, instead of `@apply` rules in the plugin's stylesheet. Its `ba-*` classes stay as hooks. Nothing changes on screen: the computed styles of every element in the field-reference and generated example documents are identical before and after.
