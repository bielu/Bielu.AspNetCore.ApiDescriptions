# Arazzo in Scalar

`Bielu.AspNetCore.Arazzo.Scalar` renders Arazzo documents in the [Scalar](https://scalar.com/) API
Reference, on the same page as the OpenAPI and AsyncAPI documents their steps call.

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

- The document's `info` and its `sourceDescriptions`.
- One sidebar entry per workflow, with one child entry per step. Sidebar clicks, deep links and
  scroll position stay in sync, as they do for operations.
- For each workflow: its inputs (nested properties fold out), shared parameters, steps, success
  and failure actions, and outputs.
- A workflow overview beside the steps: a flowchart of the steps, with arrows for `dependsOn` (or
  document order) and labeled success and failure branches for `onSuccess`/`onFailure`, ending in
  Success or Failed for `end` actions. Drag to pan, scroll to zoom, click a step to jump to it, or
  expand it to fill the window. It follows Scalar's light and dark themes.
- For each step: the operation, channel or workflow it targets, with that target's description and
  its own parameters, then the step's parameters, request body, success criteria,
  `onSuccess`/`onFailure` actions and outputs. Reusable objects (`$components.parameters.*` and the
  like) are resolved.
- Each workflow and step heading has a copy-link button. With Scalar's `classic` layout, each
  workflow can be collapsed.

A step's target is looked up in the documents loaded in the same Scalar page. The step's
source description URL is matched against the URL each Scalar source was loaded from, so a
generated `/openapi/v1.json` source description finds the `/openapi/v1.json` Scalar source. When
the target's document is not loaded, the step still shows the raw `operationId` or path.

The [`ArazzoScalar` example](https://github.com/bielu/Bielu.AspNetCore.ApiDescriptions/tree/main/src/examples/ArazzoScalar)
puts this together: two workflows mixing HTTP operations and messages, rendered at `/scalar`.

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
