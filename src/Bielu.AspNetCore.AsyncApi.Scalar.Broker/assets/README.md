# @bielu/scalar-broker

Interactive **message-broker console** plugin for the [Scalar](https://scalar.com) API Reference.

Reads the `kafka`, `mqtt` and `amqp` bindings out of your AsyncAPI document(s) and renders a console
that publishes to a channel and tails it live. A browser cannot speak these protocols, so the console
talks to a **server-side bridge** over plain HTTP — this bundle ships no broker client and is
correspondingly small.

The bridge is provided by the
[`Bielu.AspNetCore.AsyncApi.Scalar.Broker`](https://www.nuget.org/packages/Bielu.AspNetCore.AsyncApi.Scalar.Broker)
NuGet package, which also embeds and serves this bundle. **This package on its own does nothing** —
there is no broker to reach without it.

## Build outputs

| File | Shape | Loaded by |
| --- | --- | --- |
| `dist/plugin.js` | IIFE, self-installing (hooks `window.Scalar`) | The `<script>` tag the ASP.NET Core package injects. |
| `dist/plugin.mjs` | ES module, library entry | Bundlers importing `createBrokerPlugin` directly. |

The two are **not** interchangeable: `plugin.js` registers itself on load, `plugin.mjs` exports the
factory and leaves registration to you.

> Unlike `@bielu/scalar-signalr` and `@bielu/scalar-grpc`, this package ships no
> `dist/scalar-plugin.mjs` (Scalar's `pluginUrls` entry) and no standalone bundle. Those exist to
> serve the Aspire consoles, and there is no Aspire companion for the broker console — a CDN-hosted
> plugin has no server-side bridge to talk to.

## Usage

Registration is handled for you when the ASP.NET Core package serves the bundle. To register the
plugin yourself:

```ts
import { createBrokerPlugin } from '@bielu/scalar-broker'

createApiReference('#app', {
  sources: [{ title: 'Orders', url: '/asyncapi/v1.json' }],
  plugins: [createBrokerPlugin()],
})
```

## Document discovery

Documents are resolved in decreasing priority: an explicit `documents` prop; `config.broker` on the
Scalar configuration; `window.__BIELU_SCALAR_BROKER__`; otherwise auto-discovery from the Scalar
configuration's own sources.

## Repository

<https://github.com/bielu/Bielu.AspNetCore.ApiDescriptions>
