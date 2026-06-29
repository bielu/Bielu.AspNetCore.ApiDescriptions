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
<script src="https://cdn.jsdelivr.net/npm/@bielu/scalar-signalr"></script>
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

## Build

```bash
npm install
npm run build      # -> dist/bielu-scalar-signalr.js (IIFE bundle)
npm run typecheck
```

## License

MIT © Arkadiusz Biel
