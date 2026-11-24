---
"@bielu/scalar-arazzo": patch
"bielu-aspnetcore-asyncapi": patch
---

The Arazzo overview's view tabs are styled with Tailwind utilities in their template, the way Scalar writes its own components, instead of `@apply` rules in the plugin's stylesheet. The rules the expanded workflow's tab bar used to reach into the tabs with are now a `placement` of the component. Nothing changes on screen: screenshots of the tabs, inline, focused and in the expanded workflow, are identical before and after.
