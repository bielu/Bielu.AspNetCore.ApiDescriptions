---
"@bielu/scalar-arazzo": patch
"bielu-aspnetcore-asyncapi": patch
---

`import { createArazzoPlugin } from '@bielu/scalar-arazzo'`, as the npm README shows, now works: the package root is an ES module (`dist/index.mjs`) with the named exports, instead of the `<script>`-tag IIFE, which has no exports to import. Importing it registers the Arazzo document element but leaves `window.Scalar` alone, since the importer adds the plugin. The `<script>`-tag bundle stays at `dist/plugin.js` (also the package's `unpkg`/`jsdelivr` default and `@bielu/scalar-arazzo/plugin.js`), and `dist/scalar-plugin.mjs` still serves Scalar's `pluginUrls`.
