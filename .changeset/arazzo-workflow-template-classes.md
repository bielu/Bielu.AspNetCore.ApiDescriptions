---
"@bielu/scalar-arazzo": patch
"bielu-aspnetcore-asyncapi": patch
---

The Arazzo workflow's own styles (its header, the steps-and-overview layout, the overview card, the expanded overview and the "What flows" dialog) are written as Tailwind utilities in its template, as the step's already are. Nothing changes on screen: the computed styles of every element are identical before and after, at a wide and a narrow width.
