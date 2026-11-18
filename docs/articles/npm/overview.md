# NPM packages

The Scalar plugins behind the .NET packages are published to npm too. Use them from a JavaScript app,
or from any static page, with the [Scalar API Reference](https://github.com/scalar/scalar/tree/main/packages/api-reference)
and no ASP.NET Core app.

| Package | What it adds to Scalar | Setup |
|---------|------------------------|-------|
| [`@bielu/scalar-arazzo`](https://www.npmjs.com/package/@bielu/scalar-arazzo) | An Arazzo document type: workflows and steps in the sidebar, a flowchart of each workflow, and links from each step to the OpenAPI operation or AsyncAPI channel it calls. Static documents are enough. | [Arazzo for Scalar](scalar-arazzo.md) |
| [`@bielu/scalar-signalr`](https://www.npmjs.com/package/@bielu/scalar-signalr) | A live SignalR console: connect to hubs, invoke methods, watch server events. Needs an AsyncAPI document with SignalR bindings and a reachable hub. | [SignalR console](../scalar-consoles.md#signalr-console) |
| [`@bielu/scalar-grpc`](https://www.npmjs.com/package/@bielu/scalar-grpc) | A live gRPC console for unary and server-streaming methods over gRPC-Web. Needs a server with gRPC-Web enabled and the protobuf descriptor endpoint the .NET package maps. | [gRPC console](../scalar-consoles.md#grpc-console) |
| [`@bielu/scalar-broker`](https://www.npmjs.com/package/@bielu/scalar-broker) | A Kafka/MQTT/AMQP console that publishes to and tails channels. No browser speaks these protocols, so it needs the server-side bridge `Bielu.AspNetCore.AsyncApi.Scalar.Broker` mounts in an ASP.NET Core app. | [Broker console](../scalar-consoles.md#broker-console-kafka) |

All four need `@scalar/api-reference`. The Arazzo plugin needs 1.80.0 or later, the first release
with plugin `documentTypes`.

## Ways to load each package

The packages don't all ship the same entry points. These are the ones a package can be loaded through:

- **ES module import.** Import the factory from the package root and pass its plugin to
  `createApiReference({ plugins: [...] })`.
- **`<script>` bundle** (`dist/plugin.js`). Adds itself to every `Scalar.createApiReference` call on the page. Load it
  after Scalar's own script.
- **Standalone bundle** (`dist/standalone.js`). Scalar and the plugin in one file.
- **`pluginUrls` module** (`dist/scalar-plugin.mjs`). An ES module whose default export is the plugin, for Scalar's
  `pluginUrls` option. This is how the .NET Aspire packages add a plugin to the Scalar container. Use it with
  `createApiReference` from Scalar's ES module build. With `@scalar/api-reference` 1.80.0,
  `window.Scalar.createApiReference` from the CDN `<script>` did not load `pluginUrls` in our tests.

| Package | ES module import | `<script>` bundle | Standalone bundle | `pluginUrls` module |
|---------|------------------|-------------------|-------------------|---------------------|
| `@bielu/scalar-arazzo` | `import { createArazzoPlugin } from '@bielu/scalar-arazzo'` (1.1.0 or later) | `dist/plugin.js` | — | `dist/scalar-plugin.mjs` |
| `@bielu/scalar-signalr` | — (the package root is the `<script>` bundle) | `dist/plugin.js` | `dist/standalone.js` | `dist/scalar-plugin.mjs` |
| `@bielu/scalar-grpc` | `import { createGrpcPlugin } from '@bielu/scalar-grpc'` | `dist/plugin.js` | `dist/standalone.js` | `dist/scalar-plugin.mjs` |
| `@bielu/scalar-broker` | `import { createBrokerPlugin } from '@bielu/scalar-broker'`, plus the server bridge | `dist/plugin.js`, plus the server bridge | — | — |

- **Broker.** The console talks to the bridge an ASP.NET Core app mounts with `MapScalarBrokerAssets()` in every
  mode, so it ships no standalone bundle or `pluginUrls` module.
- **SignalR from a static page.** Load `dist/standalone.js`, or load `dist/plugin.js` after Scalar's own script, or
  pass `dist/scalar-plugin.mjs` to `pluginUrls`.
