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
- **Styling** (`tailwind.css`) — the shared Tailwind CSS v4 entry, built on Scalar's own
  `@scalar/themes` theme. See [Styling a console](#styling-a-console).

The document-type plugins (`@bielu/scalar-arazzo`, `@bielu/scalar-likec4`) also take their shared
parts from here, through subpaths the console plugins never import:

- **`@bielu/scalar-core/document-type`** — `documentTypeOf` and `createDocumentTypePlugin`: the
  Scalar `documentTypes` entry rendering the plugin's element, and the tagged plugin factory.
- **`@bielu/scalar-core/markdown`** — `renderMarkdown`, CommonMark through marked and DOMPurify.
  A subpath, so marked and DOMPurify stay out of the console bundles.
- **`@bielu/scalar-core/vite`** — `scalarPluginBuild`, the Vite config for the three bundles
  (`plugin.js`, `scalar-plugin.mjs`, `index.mjs`), picked by `vite build --mode`.
- **`tsconfig.plugin.json`** — the shared compiler options, extended by relative path.
- **`scripts/check-plugin-package.mjs <factoryExport>`** — the `postbuild` check that packs the
  package and resolves its exports as a consumer would.

## This package is private

It is **not published to npm**. The plugin packages depend on it with a `file:` link in their
`devDependencies` and inline it into their IIFE bundles at build time (their npm `prebuild` script
builds this package first). Two consequences for plugin packages:

- Keep the `file:` dependency in `devDependencies` — a `dependencies` entry with a `file:` path
  would break `npm install` for consumers of the published package.
- Do not re-export this package's types from a plugin package's public API: the published
  `dist/types` would then reference an unresolvable module. Declare the public-facing types locally
  (they may be structural copies) and keep `@bielu/scalar-core` imports internal.

## Styling a console

Scalar styles its UI with Tailwind CSS v4 and `@scalar/themes`, whose theme maps Tailwind's scales
onto Scalar's `--scalar-*` variables and replaces Tailwind's default palette. A console that uses
the same utilities (`text-c-2`, `bg-b-1`, `border-l-green`, `font-code`, `text-sm`, `gap-2`) gets
Scalar's colours, type scale and spacing, and follows the active Scalar theme and dark mode.

Scalar's stylesheet only contains the utilities Scalar's own markup uses, so a plugin generates its
own, scoped to its element tag the way Scalar scopes its utilities to `.scalar-app`:

```css
/* assets/src/style.css */
@import "@bielu/scalar-core/tailwind.css";

bielu-example-console {
  @tailwind utilities source("./components");
}
```

Add `tailwindcss` and `@tailwindcss/vite` to the plugin's `devDependencies`, put `tailwindcss()`
next to `vue({ customElement: true })` in its Vite config, and include the file from the component
with `<style src="../style.css"></style>`. Vue then hands the compiled CSS to
`registerConsoleElement`, which injects it once. Write class names in full rather than building
them from pieces, or Tailwind will not find them.

Buttons, text inputs and JSON editors are Scalar's own `ScalarButton`, `ScalarTextInput` and
`ScalarTextArea` from `@scalar/components`, pinned to the version `@scalar/api-reference` ships.
Their compiled templates carry Tailwind classes that Scalar's stylesheet does not all contain, so a
console that uses them adds an `@source` for those component folders next to its scoped block (see
the consoles' `style.css`). Selects stay native: `ScalarListbox` would add about 50 KB to a bundle.

The other patterns the consoles share are `bielu-*` utilities in `tailwind.css`: `bielu-card`,
`bielu-caption`, `bielu-select`, `bielu-tag`, `bielu-pill`, `bielu-result` and `bielu-log`. They
leave colour to the markup, so a shared utility never competes with a colour utility. Colour
variants (method tags, log badges, result borders) are `cva` recipes from
`@scalar/use-hooks/useBindCx`, the way Scalar's components define theirs.

## Building

```bash
npm install
npm run build   # tsc → dist/ (ESM + .d.ts)
```
