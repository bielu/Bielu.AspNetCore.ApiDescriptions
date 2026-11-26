# @bielu/scalar-likec4

A LikeC4 document type for the [Scalar API Reference](https://github.com/scalar/scalar/tree/main/packages/api-reference)
(1.80.0 or later). It draws the views of a [LikeC4](https://likec4.dev) architecture model next to the API
documents they describe:

- each view gets a sidebar entry;
- each view is drawn as LikeC4 laid it out, in Scalar's theme;
- dynamic views can be drawn as sequence diagrams;
- an element or step that opens another view links to it.

It reads the JSON `likec4 export json` writes, which is already laid out, so no layout engine runs in the
browser.

```bash
npx likec4 export json -o public/architecture.likec4.json architecture
npm install @scalar/api-reference @bielu/scalar-likec4
```

```js
import { createApiReference } from '@scalar/api-reference'
import '@scalar/api-reference/style.css'
import { createLikeC4Plugin } from '@bielu/scalar-likec4'

createApiReference('#app', {
  sources: [
    { title: 'Architecture', url: '/architecture.likec4.json' },
    { title: 'Orders API', url: '/asyncapi/orders.json' },
  ],
  plugins: [createLikeC4Plugin()],
})
```

The package also ships:

- **`dist/plugin.js`**: a `<script>` bundle that adds the plugin to every `Scalar.createApiReference` call
  on the page;
- **`dist/scalar-plugin.mjs`**: a module for Scalar's `pluginUrls`.

See [LikeC4 for Scalar](https://github.com/bielu/Bielu.AspNetCore.ApiDescriptions/blob/main/docs/articles/npm/scalar-likec4.md).
