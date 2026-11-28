---
"@bielu/scalar-arazzo": patch
"@bielu/scalar-likec4": patch
"bielu-aspnetcore-asyncapi": patch
---

The Arazzo and LikeC4 Scalar plugins now take their shared document layout (page width and padding, title, kicker, muted text, Markdown, section headings) from one stylesheet in the private `@bielu/scalar-core` package, as Tailwind utilities each plugin applies in its own selectors. The compiled styles are the same.
