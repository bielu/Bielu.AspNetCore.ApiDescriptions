# @bielu/scalar-core

Shared internals for the Bielu Scalar AsyncAPI console plugins (`@bielu/scalar-signalr`,
`@bielu/scalar-grpc`, ...). It contains everything that is protocol-agnostic:

- **Document discovery** (`discovery.ts`) — deriving the AsyncAPI documents to scan from the Scalar
  configuration (with the same title slugging Scalar uses), an inline plugin config, or a window
  global injected by the .NET packages.
- **Document loading** (`documents.ts`) — fetching/parsing AsyncAPI documents with timeouts, JSON
  pointer / `$ref` resolution, server host resolution and security-scheme extraction.
- **Schema examples** (`schema-example.ts`) — generating a representative example payload from a
  JSON schema to prefill the consoles' editors.
- **Auth-state capture** (`auth.ts`) — storing the PluginAuthState exposed by the custom Scalar
  build and resolving the schemes/secrets the user selected for a document. The mapping onto a
  transport (query params for WebSocket, headers for gRPC-Web, ...) stays in each plugin.
- **Scalar bootstrap** (`plugin.ts`, `bootstrap.ts`) — creating the Scalar plugin (sidebar entry +
  `content.end` view rendering the console's custom element), registering the Web Component, and
  hooking `window.Scalar.createApiReference`.

## This package is private

It is **not published to npm**. The plugin packages depend on it with a `file:` link in their
`devDependencies` and inline it into their IIFE bundles at build time (their npm `prebuild` script
builds this package first). Two consequences for plugin packages:

- Keep the `file:` dependency in `devDependencies` — a `dependencies` entry with a `file:` path
  would break `npm install` for consumers of the published package.
- Do not re-export this package's types from a plugin package's public API: the published
  `dist/types` would then reference an unresolvable module. Declare the public-facing types locally
  (they may be structural copies) and keep `@bielu/scalar-core` imports internal.

## Building

```bash
npm install
npm run build   # tsc → dist/ (ESM + .d.ts)
```
