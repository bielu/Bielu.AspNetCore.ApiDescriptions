# Arazzo in Scalar

`Bielu.AspNetCore.Arazzo.Scalar` renders Arazzo documents in the [Scalar](https://scalar.com/) API
Reference, on the same page as the OpenAPI and AsyncAPI documents their steps call.

![An Arazzo workflow in Scalar: the source descriptions with their specs, the workflow's inputs and first step, and the workflow overview flowchart with a marker per spec](../../images/arazzo/scalar-workflow-overview.jpg)

> [!IMPORTANT]
> The plugin uses Scalar's plugin `documentTypes` API, first released in `@scalar/api-reference` 1.80.0
> ([scalar/scalar#10816](https://github.com/scalar/scalar/pull/10816)). `Scalar.AspNetCore` bundles that version from
> 3.0.1 on, and this package depends on it. Older Scalar versions ignore the plugin and show an Arazzo source as an
> empty OpenAPI document.

## Setup

```sh
dotnet add package Bielu.AspNetCore.Arazzo.Scalar
```

```csharp
builder.Services.AddArazzo(options =>
{
    options.WithInfo("Order workflows", "1.0.0");
    options.AddOpenApiSource("orders", "v1");
    options.AddAsyncApiSource("events", "v1");
    options.AddWorkflow("placeOrder", workflow => workflow
        .Step("create", step => step.Operation("createOrder"))
        .Step("announce", step => step.Channel("events", "orderPlaced", ArazzoStepAction.Send)));
});

var app = builder.Build();

app.MapOpenApi();
app.MapAsyncApi();
app.MapArazzo();
app.MapScalarArazzoAssets();

app.MapScalarApiReference(options => options
    .AddDocument("v1", "Orders")
    .AddAsyncApiDocument("v1", "Events")
    .AddArazzoDocument("v1", "Workflows")
    .WithArazzo());
```

`AddArazzoDocument` adds the document `MapArazzo()` serves (`/arazzo/{documentName}.json` by
default) as a Scalar source. `WithArazzo` adds the plugin script, which `MapScalarArazzoAssets`
serves from `/bielu/scalar/arazzo/plugin.js`.

## What is rendered

Step targets emphasize the HTTP method and path or channel action and address. Source versions remain
in the Sources table and target tooltips; steps use compact spec labels. Message payload properties
share the page surface, and nested properties, examples and extensions use consistent disclosure controls.

An AsyncAPI step shows the message it receives, with its payload, next to the step's correlation ID,
timeout, criteria and outputs:

![A step receiving the paymentCompleted message: spec badge, channel link, the message payload's properties, a JSONPath success criterion and a failure branch](../../images/arazzo/scalar-asyncapi-step.jpg)

With its steps collapsed, a workflow reads as an outline of what each step calls:

![The placeOrder workflow with its five steps collapsed, each showing its spec and target in one line, beside the workflow overview](../../images/arazzo/scalar-collapsed-steps.jpg)

- The document's `info`, its `$self` URL (Arazzo 1.1) and its `sourceDescriptions`. Each source shows its spec and version, whether
  it is loaded in the page, and how many steps use it, and the header counts the steps per spec.
- A filter box under the header that narrows the workflows and steps by id, summary, description,
  target or action (every word must match), keeping each step's number. Scalar's own search (Ctrl+K)
  does not index plugin documents yet.
- One sidebar entry per workflow, with one child entry per step. Sidebar clicks, deep links and
  scroll position stay in sync, as they do for operations.
- For each workflow: its inputs (nested properties fold out), shared parameters, steps, success
  and failure actions, and outputs.
- A workflow overview beside the steps: a flowchart of the steps, with arrows for `dependsOn` (or
  document order) and labeled success and failure branches for `onSuccess`/`onFailure`, ending in
  Success or Failed for `end` actions. Drag to pan, scroll to zoom, click a step to jump to it, or
  expand it to fill the window. It follows Scalar's light and dark themes. From the keyboard, the
  focused overview pans with the arrow keys, zooms with `+`/`-` and resets with `0`; Tab reaches
  each step box, and Enter opens the step. Each step box has a
  colored marker for the spec its target is described in, explained by a legend in the corner.
- For each step: the operation, channel or workflow it targets, with that target's description and
  its own parameters. An AsyncAPI step also shows the message it sends or receives: its payload as
  a property list (references followed into the AsyncAPI document), its content type, and its first
  example payload; a payload in another schema format, such as Avro, is shown as written. Then come
  the step's parameters, request body, success criteria,
  `onSuccess`/`onFailure` actions and outputs. Arazzo 1.1 steps also show their `timeout` and
  `correlationId`. A criterion that isn't a simple condition is labeled with its language
  (`regex`, `jsonpath`, `xpath`, plus the version when given) and the context it reads, and an
  action that starts a workflow lists the `parameters` it passes. A request body lists its
  `replacements` (target, its selector type, and the value written there). Wherever a value can be
  an Arazzo 1.1 Selector Object (outputs, parameters, replacements), it reads as
  "`$.lines[0]` on `$response.body`" with its language as a badge, rather than as raw JSON. Reusable objects (`$components.parameters.*` and the
  like) are resolved.
- Each step target is labeled with its spec: `OpenAPI`, `AsyncAPI` or `Workflow`, with the version in its tooltip and the Sources table. Channel
  steps also say whether they send or receive. See [Which spec a step targets](#which-spec-a-step-targets).
- The reusable `components` (input schemas, parameters, success and failure actions) in a section
  of their own after the workflows, with a sidebar entry.
- Specification Extensions (`x-*` fields) on the document, `info`, source descriptions, workflows,
  steps, request bodies, actions and components, in a collapsed "Extensions" list. Scalar's own
  `x-scalar-*` fields are left out.
- Each step can be collapsed with the chevron beside its heading, and a workflow's "Collapse steps"
  button folds all of its steps at once. A collapsed step still shows a one-line summary of what it
  targets (spec, action and target), and clicking a step in the overview opens it.
- Each workflow and step heading has a copy-link button. With Scalar's `classic` layout, each
  workflow can be collapsed.

A step's target is looked up in the documents loaded in the same Scalar page. The step's
source description URL is matched against the URL each Scalar source was loaded from, so a
generated `/openapi/v1.json` source description finds the `/openapi/v1.json` Scalar source. When
the target's document is not loaded, the step still shows the raw `operationId` or path.

A resolved target is a link. It names the target (its `operationId`, or the AsyncAPI operation or
channel name) rather than the raw `{$sourceDescriptions.…}` reference, and opens it in its own
document: the OpenAPI operation, the AsyncAPI operation, or the channel. Scalar picks the document it
shows when the page loads, so following the link reloads the page at the target's deep link. Back
and Forward switch documents the same way, and Ctrl-click opens the target in a new tab. A step that
invokes another workflow links to that workflow's section. The links use the ids of Scalar's own
sidebar, so they follow any `generateOperationSlug` the page sets; they assume Scalar's default hash
routing.

The [`ArazzoScalar` example](https://github.com/bielu/Bielu.AspNetCore.ApiDescriptions/tree/main/src/examples/ArazzoScalar)
puts this together: two workflows mixing HTTP operations and messages, rendered at `/scalar`. They
are built with the fluent API, including payload replacements, Selector Object parameters and
outputs, workflow outputs a later workflow reads, and a reusable failure action. The example also
serves a hand-written "Arazzo field reference" document (`wwwroot/fixtures`) that uses every field of
Arazzo 1.1, so the view is checked against fields the example's workflows don't use. For what the
builders can and cannot produce, see [Authoring coverage](overview.md#authoring-coverage).

## Which spec a step targets

An Arazzo workflow can mix HTTP calls (OpenAPI) and messages (AsyncAPI), so each step says which
one it targets. The spec is worked out in this order:

1. When the target is found in a loaded document, that document's `openapi`, `swagger` or
   `asyncapi` field gives the spec and its version.
2. Otherwise the step's source description decides: the one named by
   `{$sourceDescriptions.NAME.url}#/…` or by a qualified `$sourceDescriptions.NAME.operationId`, through
   its `type`. A source without a `type` falls back to the pointer: `/paths/…` is OpenAPI, and
   `/channels/…` and `/operations/…` are AsyncAPI. A `channelPath` step is always AsyncAPI.
3. A bare `operationId` that is not loaded takes the spec all the source descriptions share. When
   they mix specs, no badge is shown.

In cases 2 and 3 the badge has a dashed border and reads "not loaded". A step that invokes another
workflow is labeled `Workflow`. The generated `sourceDescriptions` from `AddOpenApiSource` and
`AddAsyncApiSource` always set `type`, so this fallback works for them.

The spec is also shown as a color (blue for OpenAPI, purple for AsyncAPI, gray for workflows),
taken from Scalar's theme, but the badge text and the diagram's legend and tooltips always name it,
so the color is never the only cue.

## Without ASP.NET Core

The bundle is also published to npm as `@bielu/scalar-arazzo`:

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

`dist/scalar-plugin.mjs` can be passed to Scalar's `pluginUrls` instead.
