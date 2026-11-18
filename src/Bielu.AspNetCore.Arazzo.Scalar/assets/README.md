# @bielu/scalar-arazzo

An [Arazzo](https://spec.openapis.org/arazzo/latest.html) document type for the
[Scalar](https://scalar.com/) API Reference. Scalar claims any source with an `arazzo` version,
lists its workflows and steps in the sidebar, and renders them, each workflow next to a flowchart of
its steps (laid out with [dagre](https://github.com/dagrejs/dagre)). Each step's target
(`operationId`, `operationPath`, `channelPath` or `workflowId`) is resolved in the OpenAPI and
AsyncAPI documents loaded in the same Scalar page.

Targets emphasize the HTTP method and path or channel action and address. Compact step labels keep
the view quiet; full spec versions remain in Sources and target tooltips. Payload properties share
the page surface, with consistent disclosures for nested properties, examples and extensions.

Requires `@scalar/api-reference` 1.80.0 or later, the first version with plugin `documentTypes`
([scalar/scalar#10816](https://github.com/scalar/scalar/pull/10816)).

## What it shows

- Every field of Arazzo 1.0 and 1.1: workflows, steps, inputs, parameters, request bodies with
  replacements, success criteria with their expression language, success and failure actions,
  outputs, Selector Objects, reusable `components`, `$self`, and `x-*` extensions.
- Which spec each step targets (`OpenAPI 3.1.1`, `AsyncAPI 3.0.0` or `Workflow`, plus send/receive),
  on the step, on the overview flowchart, and per source description.
- Links from each step to the operation or channel it calls in its own document, and for AsyncAPI
  steps the message payload they send or receive.
- Collapsible steps and a filter for workflows and steps. The overview can be used from the
  keyboard, and the layout works at phone width. A malformed document renders what it can instead
  of failing.

## Versioning

The plugin follows semantic versioning from 1.0.0. Its public surface is `createArazzoPlugin`,
`arazzoDocumentType`, the `<script>` and `pluginUrls` entry points, and the exported types. The
rendered markup and its CSS classes are not part of it.

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

The package root is an ES module: importing it registers the Arazzo document element, and the
plugin is added where you pass it, as above.

As a `<script>` tag (`dist/plugin.js`, the package's default file on unpkg and jsDelivr), which hooks
`window.Scalar.createApiReference` and adds the plugin to every API Reference on the page; or through
Scalar's `pluginUrls` with `dist/scalar-plugin.mjs`, whose default export is the plugin.

ASP.NET Core apps use the
[Bielu.AspNetCore.Arazzo.Scalar](https://www.nuget.org/packages/Bielu.AspNetCore.Arazzo.Scalar)
package, which embeds and serves this bundle.
