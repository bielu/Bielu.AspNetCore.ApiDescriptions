# Arazzo for Scalar

[`@bielu/scalar-arazzo`](https://www.npmjs.com/package/@bielu/scalar-arazzo) is a plugin for the
[Scalar API Reference](https://github.com/scalar/scalar/tree/main/packages/api-reference). It renders
[Arazzo](https://spec.openapis.org/arazzo/latest.html) documents next to the OpenAPI and AsyncAPI documents their steps call.
Scalar lists each workflow and its steps in the sidebar and draws a flowchart of the workflow. Each step
links to the operation or channel it calls. This guide runs it from static files in a Vite app, with no
server code. In an ASP.NET Core app, use [Arazzo in Scalar](../arazzo/scalar.md) instead: the NuGet package
serves this same plugin.

## Requirements

- `@scalar/api-reference` **1.80.0 or later**. Plugin document types first shipped in this release. An older
  Scalar shows an Arazzo source as an empty OpenAPI document.
- `@bielu/scalar-arazzo` **1.1.0 or later** for the `import` below and for the Pages view. 1.0.0 offers only the
  `<script>` and `pluginUrls` entry points.

## Quickstart

Create a Vite app and install Scalar and the plugin:

```sh
npm create vite@latest pet-workflows -- --template vanilla
cd pet-workflows
npm install @scalar/api-reference @bielu/scalar-arazzo
```

Add two documents to `public/`. `public/openapi.json` describes the API:

```json
{
  "openapi": "3.1.0",
  "info": { "title": "Pets", "version": "1.0.0" },
  "paths": {
    "/pets": {
      "get": { "operationId": "listPets", "summary": "List the pets", "responses": { "200": { "description": "The pets" } } }
    },
    "/pets/{petId}/adopt": {
      "post": {
        "operationId": "adoptPet",
        "summary": "Adopt a pet",
        "parameters": [{ "name": "petId", "in": "path", "required": true, "schema": { "type": "string" } }],
        "responses": { "204": { "description": "Adopted" } }
      }
    }
  }
}
```

`public/arazzo.json` describes a workflow whose steps call those operations:

```json
{
  "arazzo": "1.0.1",
  "info": { "title": "Pet workflows", "version": "1.0.0" },
  "sourceDescriptions": [{ "name": "pets", "url": "/openapi.json", "type": "openapi" }],
  "workflows": [
    {
      "workflowId": "adoptPet",
      "summary": "Find a pet and adopt it",
      "steps": [
        {
          "stepId": "findPets",
          "operationId": "listPets",
          "successCriteria": [{ "condition": "$statusCode == 200" }],
          "outputs": { "petId": "$response.body#/0/id" }
        },
        {
          "stepId": "adopt",
          "operationId": "adoptPet",
          "parameters": [{ "name": "petId", "in": "path", "value": "$steps.findPets.outputs.petId" }],
          "successCriteria": [{ "condition": "$statusCode == 204" }]
        }
      ]
    }
  ]
}
```

Replace `src/main.js` with this. The template's `index.html` already has the `#app` element and loads this
file:

```js
import { createApiReference } from '@scalar/api-reference'
import '@scalar/api-reference/style.css'
import { createArazzoPlugin } from '@bielu/scalar-arazzo'

createApiReference('#app', {
  sources: [
    { title: 'Workflows', url: '/arazzo.json' },
    { title: 'Pets', url: '/openapi.json' },
  ],
  plugins: [createArazzoPlugin()],
})
```

Run `npm run dev` and open the URL Vite prints. You should see:

- **Workflows** selected in Scalar's document picker. The sidebar lists the workflow by its summary,
  "Find a pet and adopt it", with its two steps, `findPets` and `adopt`.
- On the right, the workflow overview: `findPets` → `adopt` → **Success**. Each step box carries a blue
  marker for OpenAPI, and selecting a box scrolls to that step.
- Under each step, its target: `GET /pets` for `findPets` and `POST /pets/{petId}/adopt` for `adopt`.
  Selecting the target opens that operation in the **Pets** document.

A step finds its operation by `operationId` in any document loaded on the page. A step that names its source
(`$sourceDescriptions.pets.listPets`, or `operationPath` with `{$sourceDescriptions.pets.url}`) is looked up
in that source. Load the source with Scalar for the link to work.

## Other ways to load the plugin

### `<script>` tag

The `<script>` bundle (`dist/plugin.js`, the package's default file on jsDelivr and unpkg) adds the plugin
to every `Scalar.createApiReference` call on the page. Load it after Scalar's own script:

```html
<div id="app"></div>
<script src="https://cdn.jsdelivr.net/npm/@scalar/api-reference@1.80.0"></script>
<script src="https://cdn.jsdelivr.net/npm/@bielu/scalar-arazzo@1"></script>
<script>
  Scalar.createApiReference('#app', {
    sources: [
      { title: 'Workflows', url: '/arazzo.json' },
      { title: 'Pets', url: '/openapi.json' },
    ],
  })
</script>
```

To set [options](#options) on this path, assign them to `window.__BIELU_SCALAR_ARAZZO__` before the plugin's
script runs.

### `pluginUrls`

`dist/scalar-plugin.mjs` is an ES module whose default export is the plugin. Pass it to Scalar's
`pluginUrls`, which loads the module before the API Reference mounts. This path needs no import of
the plugin, so the configuration can stay plain JSON:

```js
import { createApiReference } from '@scalar/api-reference'
import '@scalar/api-reference/style.css'

createApiReference('#app', {
  sources: [
    { title: 'Workflows', url: '/arazzo.json' },
    { title: 'Pets', url: '/openapi.json' },
  ],
  pluginUrls: ['https://cdn.jsdelivr.net/npm/@bielu/scalar-arazzo@1/dist/scalar-plugin.mjs'],
})
```

The module reads its options from its own query string (`scalar-plugin.mjs?pageExtension=x-page`).

> [!NOTE]
> With `@scalar/api-reference` 1.80.0, `createApiReference` from the ES module loads `pluginUrls`. The
> `window.Scalar.createApiReference` from the CDN `<script>` did not request the module in our tests. On a
> page built from `<script>` tags, use the `<script>` bundle above instead.

## Technical and Pages views

The overview can also show the pages a user goes through, for readers who think in screens rather than
operations. Describe the app's pages on the document. Then give each step the page the user is on, and each
action the page the user lands on. Add plain-language labels where you want them. Here the quickstart's
workflow gets a page registry, a labelled step and a landing page. Only the changed parts are shown:

```json
{
  "arazzo": "1.0.1",
  "x-pages": {
    "catalog": { "title": "Pet catalog", "route": "/pets" },
    "adoption": { "title": "Adoption", "route": "/pets/{petId}/adopt" },
    "welcome": { "title": "Welcome home", "route": "/adopted" }
  },
  "workflows": [
    {
      "workflowId": "adoptPet",
      "x-label": "Adopt a pet",
      "steps": [
        { "stepId": "findPets", "operationId": "listPets", "x-page": "catalog", "x-label": "Browse the pets" },
        {
          "stepId": "adopt",
          "operationId": "adoptPet",
          "x-page": "adoption",
          "onSuccess": [{ "name": "adopted", "type": "end", "x-page": "welcome", "x-label": "Pet adopted" }]
        }
      ]
    }
  ]
}
```

Then tell the plugin which extensions hold these:

```js
createArazzoPlugin({
  pagesExtension: 'x-pages', // on the document: page id → { title, route, description }
  pageExtension: 'x-page', // on a step: the page the user is on; on an action: the page they land on
  labelExtension: 'x-label', // on a workflow, step or action: a plain-language label
})
```

- **When the toggle appears.** The overview gets a **Technical | Pages** toggle when `pagesExtension`,
  `pageExtension` or `labelExtension` is set and the document uses it: a page registry, or a page or label on
  a workflow, a step or an action a step runs. Technical stays the default, and the choice is remembered in
  the browser.
- **What Pages shows.** `findPets` sits in a "Pet catalog · /pets" band and reads "Browse the pets".
  `adopt` has no label, so it falls back to its `stepId` (shown in the tooltip either way), in an
  "Adoption" band. The success end is named "Welcome home ✓".
- **Release.** Pages needs `@bielu/scalar-arazzo` 1.1.0 or later.

See [Pages view](../arazzo/scalar.md#pages-view) for the full rules and a screenshot.

## Options

| Option | What it does |
|--------|--------------|
| `endLabelExtension` | Names an `end` failure action's end node after this extension's value instead of the action's name. Also used as an action's landing page when `pageExtension` doesn't give one. |
| `pagesExtension` | The document extension holding the page registry. |
| `pageExtension` | The step and action extension naming a page. |
| `labelExtension` | The workflow, step and action extension holding a plain-language label. |
| `actorExtension` | The step and workflow extension naming who makes a step's call in the Sequence view: a source description's name, or anyone else's. The client when unset. |

Each value must be a specification extension name: `x-` followed by a name. Anything else is ignored. The
options can be passed in three ways, and the first one present wins:

1. `createArazzoPlugin(options)`.
2. `window.__BIELU_SCALAR_ARAZZO__`.
3. The `scalar-plugin.mjs` query string.

## Troubleshooting

- **The Arazzo source shows as an empty OpenAPI document.** Either Scalar is older than 1.80.0, or the plugin
  never reached it. Check that `plugins`, the `<script>` bundle or `pluginUrls` is in place on the page that
  calls `createApiReference`.
- **A step's target says "not loaded".** The step's operation isn't in any document Scalar has loaded. Add its
  source to `sources`, using the URL its `sourceDescriptions` entry gives.
- **No Technical | Pages toggle.** Check that the options name the extensions the document actually uses, with
  the `x-` prefix.
