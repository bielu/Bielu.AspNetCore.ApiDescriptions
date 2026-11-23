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

## Quick start

```sh
npm install @scalar/api-reference @bielu/scalar-arazzo
```

```js
import { createApiReference } from '@scalar/api-reference'
import '@scalar/api-reference/style.css'
import { createArazzoPlugin } from '@bielu/scalar-arazzo'

createApiReference('#app', {
  sources: [
    { title: 'Workflows', url: '/arazzo.json' }, // an Arazzo document
    { title: 'Pets', url: '/openapi.json' }, // the OpenAPI document its steps call
  ],
  plugins: [createArazzoPlugin()],
})
```

The [Arazzo for Scalar guide](https://apidescriptions.bielu.pl/articles/npm/scalar-arazzo.html) has the full walkthrough, with sample documents and what
to expect. It also covers the `<script>` and `pluginUrls` entry points, the options (including the
Technical | Pages views) and troubleshooting.

## What it shows

- Every field of Arazzo 1.0 and 1.1: workflows, steps, inputs, parameters, request bodies with
  replacements, success criteria with their expression language, success and failure actions,
  outputs, Selector Objects, reusable `components`, `$self`, and `x-*` extensions.
- Runtime Expressions as links to the input, step output, workflow output or component they read,
  with the ones that point at something the document doesn't declare marked, and a "Used by" list
  under each output.
- Problems found against the loaded OpenAPI and AsyncAPI documents (missing targets, parameters
  and statuses, broken references, steps that never run), listed under the header, on each step and
  on its box in the overview.
- Which spec each step targets (`OpenAPI 3.1.1`, `AsyncAPI 3.0.0` or `Workflow`, plus send/receive),
  on the step, on the overview flowchart, and per source description.
- Links from each step to the operation or channel it calls in its own document. For AsyncAPI
  steps, the message payload they send or receive; for OpenAPI steps, the operation's request body
  and responses, with the responses the step's criteria and actions check for marked.
- Workflows a step or a `goto`/`retry` action invokes as nodes of their own in the flowchart, with an
  arrow back for a `retry`. A `+` on a workflow node shows its steps inside the flowchart
  (recursively, never re-expanding a workflow already on the path), and a step that invokes one can
  list that workflow's steps inline. This works for workflows of the same document and, through
  `$sourceDescriptions.<name>.<workflowId>`, of another Arazzo document loaded on the page. Distinct `end` failure actions get end nodes of their own.
- An example input for each workflow, made from its inputs schema, to copy.
- A Sequence view: the client, each service or broker it talks to, and the requests, responses and
  messages between them in run order, with each workflow a step invokes drawn in place as a numbered stage.
- A Data flow view of the overview: which inputs and outputs each step reads, and from where.
- Export of the overview as SVG or PNG, or as Mermaid source to paste into Markdown.
- Collapsible steps and a filter for workflows and steps. The overview can be used from the
  keyboard, and the layout works at phone width. A malformed document renders what it can instead
  of failing.

## Versioning

The plugin follows semantic versioning from 1.0.0. Its public surface is `createArazzoPlugin`,
`arazzoDocumentType`, the `<script>` and `pluginUrls` entry points, and the exported types. The
rendered markup and its CSS classes are not part of it.

ASP.NET Core apps use the
[Bielu.AspNetCore.Arazzo.Scalar](https://www.nuget.org/packages/Bielu.AspNetCore.Arazzo.Scalar)
package, which embeds and serves this bundle.
