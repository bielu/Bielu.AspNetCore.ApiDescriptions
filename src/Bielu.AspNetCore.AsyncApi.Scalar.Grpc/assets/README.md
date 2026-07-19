# @bielu/scalar-grpc

An interactive **gRPC console** plugin for the [Scalar](https://scalar.com) API Reference.

It reads the `grpc` bindings from your AsyncAPI document(s) and renders a panel where you can pick
a service, edit a JSON request prefilled from the payload schema, and invoke unary and
server-streaming methods live — over **gRPC-Web** (browsers cannot speak native gRPC). Client- and
bidirectional-streaming methods are rendered as documentation with a "not invokable from the
browser" badge.

Because AsyncAPI payload schemas are JSON Schema (no protobuf field numbers), the console encodes
wire messages from real protobuf descriptors fetched from the companion .NET package's
`{assetsPath}/descriptors` endpoint (a serialized `FileDescriptorSet`), using
`@bufbuild/protobuf` + `@connectrpc/connect-web` — fully dynamic, no codegen in the browser.

This package is produced from, and consumed by, the
[`Bielu.AspNetCore.AsyncApi.Scalar.Grpc`](https://github.com/bielu/Bielu.AspNetCore.AsyncApi)
.NET package (which embeds the built bundle), but it is also published standalone to npm so it can
be loaded from a CDN or bundled by hand.

## Server prerequisites

The target ASP.NET Core app must:

1. enable gRPC-Web: `app.UseGrpcWeb(new GrpcWebOptions { DefaultEnabled = true })` (package
   `Grpc.AspNetCore.Web`), and
2. serve the descriptor endpoint: `app.MapScalarGrpcAssets()` (package
   `Bielu.AspNetCore.AsyncApi.Scalar.Grpc`).

When the page and the gRPC server are on different origins, CORS must allow the gRPC-Web response
headers: `grpc-status` and `grpc-message` need to be exposed
(`policy.WithExposedHeaders("Grpc-Status", "Grpc-Message", "Grpc-Encoding", "Grpc-Accept-Encoding")`).

## Standalone usage (CDN)

```html
<div id="app"></div>
<script src="https://cdn.jsdelivr.net/npm/@bielu/scalar-grpc"></script>
<script>
  window.__BIELU_SCALAR_GRPC__ = {
    documents: [{ name: 'grpc', url: '/asyncapi/grpc.json' }],
  }
  Scalar.createApiReference('#app', {
    sources: [{ url: '/asyncapi/grpc.json' }],
  })
</script>
```

The gRPC documents can be provided either via the global `window.__BIELU_SCALAR_GRPC__` or inline
on the Scalar config as `config.grpc`.

## Programmatic usage

```ts
import { createGrpcPlugin } from '@bielu/scalar-grpc'

Scalar.createApiReference('#app', {
  sources: [{ url: '/asyncapi/grpc.json' }],
  plugins: [createGrpcPlugin()],
})
```

## Auth

The console picks up the credentials entered in Scalar's Authentication panel (Scalar ≥ 2.16.12)
and maps them onto gRPC-Web call metadata: header API keys, `Authorization: Bearer …` and HTTP
Basic all map directly (gRPC-Web metadata is plain HTTP headers). Additional per-call metadata can
be entered in the console's metadata editor.

## Building

```bash
npm install
npm run build   # → dist/plugin.js (IIFE) + dist/types
```

The build inlines the private `@bielu/scalar-core` workspace package (document discovery, auth
state, schema examples, Scalar bootstrap) — see that package's README for the shared contract.
