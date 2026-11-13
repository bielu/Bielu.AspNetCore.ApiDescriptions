# @bielu/scalar-arazzo

An [Arazzo](https://spec.openapis.org/arazzo/latest.html) document type for the
[Scalar](https://scalar.com/) API Reference. Scalar claims any source with an `arazzo` version,
lists its workflows and steps in the sidebar, and renders them. Each step's target
(`operationId`, `operationPath`, `channelPath` or `workflowId`) is resolved in the OpenAPI and
AsyncAPI documents loaded in the same Scalar page.

Requires a Scalar version with plugin `documentTypes`
([scalar/scalar#10816](https://github.com/scalar/scalar/pull/10816)).

## Usage

As a plugin:

```ts
import { createArazzoPlugin } from '@bielu/scalar-arazzo'

Scalar.createApiReference('#app', {
  sources: [
    { title: 'Pets', url: '/openapi/v1.json' },
    { title: 'Workflows', url: '/arazzo/v1.json' },
  ],
  plugins: [createArazzoPlugin()],
})
```

As a `<script>` tag (`dist/plugin.js`), which hooks `window.Scalar.createApiReference` and adds the
plugin to every API Reference on the page; or through Scalar's `pluginUrls` with
`dist/scalar-plugin.mjs`.

ASP.NET Core apps use the
[Bielu.AspNetCore.Arazzo.Scalar](https://www.nuget.org/packages/Bielu.AspNetCore.Arazzo.Scalar)
package, which embeds and serves this bundle.
