---
"@bielu/scalar-arazzo": patch
"@bielu/scalar-likec4": patch
"bielu-aspnetcore-asyncapi": patch
---

The Arazzo and LikeC4 Scalar plugins now share their build setup, document-type plugin factory and Markdown rendering through the private `@bielu/scalar-core` package instead of keeping a copy each. Each package builds its three bundles from one `vite.config.ts` (`vite build --mode module` / `--mode library`). No change to what the plugins render or export.
