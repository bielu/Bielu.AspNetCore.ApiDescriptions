# @bielu/scalar-signalr

An interactive **SignalR console** plugin for the [Scalar](https://scalar.com) API Reference.

It reads the SignalR bindings from your AsyncAPI document(s) and renders a panel where you can
connect to a hub, invoke client-to-server methods, and watch server-to-client events live. It is
built as a drop-in replacement for the default `scalar.js` bundle: loading it re-exposes
`window.Scalar.createApiReference` with the SignalR plugin already registered.

This package is produced from, and consumed by, the
[`Bielu.AspNetCore.AsyncApi.Scalar.SignalR`](https://github.com/bielu/Bielu.AspNetCore.AsyncApi)
.NET package (which embeds the built bundle), but it is also published standalone to npm so it can
be loaded from a CDN or bundled by hand.

## Standalone usage (CDN)

```html
<div id="app"></div>
<!-- Replaces the default scalar.js bundle -->
<script src="https://cdn.jsdelivr.net/npm/@bielu/scalar-signalr/dist/standalone.js"></script>
<script>
  window.__BIELU_SCALAR_SIGNALR__ = {
    documents: [{ name: 'signalr', url: '/asyncapi/signalr.json' }],
  }
  Scalar.createApiReference('#app', {
    sources: [{ url: '/asyncapi/signalr.json' }],
  })
</script>
```

The SignalR documents can be provided either via the global `window.__BIELU_SCALAR_SIGNALR__`
or inline on the Scalar config as `config.signalr`.

## Programmatic usage

```ts
import { createApiReference, createSignalRPlugin } from '@bielu/scalar-signalr'

// Either use the wrapper (auto-registers the plugin) …
createApiReference('#app', { sources: [{ url: '/asyncapi/signalr.json' }] })

// … or register the plugin yourself on the stock Scalar API Reference:
// plugins: [createSignalRPlugin([{ name: 'signalr', url: '/asyncapi/signalr.json' }])]
```

## Auth integration

Scalar owns the top-level Authentication UI. When the user selects a security scheme there, the
SignalR console picks up the credentials automatically at **Connect** time — no separate login step
is needed.

**Requires Scalar ≥ 2.16.12** (the plugin auth-state API, upstreamed in
[scalar/scalar#9639](https://github.com/scalar/scalar/pull/9639)). On older Scalar versions that
predate the API, the console connects without credentials (graceful no-op).

### How credentials are mapped to the SignalR client

| Scheme | Location | SignalR mapping |
|---|---|---|
| `apiKey` | `query` | Key appended to hub URL as `?<name>=<value>` |
| `apiKey` | `header` | **Warning logged** — browser WS/SSE cannot set headers; falls back to query |
| `http` bearer | — | `accessTokenFactory: () => token` |
| `oauth2` / `openIdConnect` | — | `accessTokenFactory: () => token` |
| `http` basic | — | **Warning logged** — not sendable over WS/SSE; connect proceeds without auth |

The scheme definition is read from `components.securitySchemes` in the AsyncAPI document. The
console matches the Scalar document name to the selected scheme; if no exact match is found it
falls back to the sole document in Scalar's exported auth state.

## Build

```bash
npm install
npm run build      # -> dist/plugin.js (IIFE) + dist/standalone.js (Scalar + plugin) + dist/types
npm run typecheck
```

`dist/plugin.js` hooks an already-loaded Scalar (`MapScalarSignalRAssets()` serves it next to
Scalar's own bundle). `dist/standalone.js` prepends Scalar's prebuilt browser bundle for pages
where nothing else loads Scalar — it is what `ScalarAspireOptions.BundleUrl` is pointed at in the
Aspire setup, since that option *replaces* Scalar's bundle.

## License

MIT © Arkadiusz Biel
