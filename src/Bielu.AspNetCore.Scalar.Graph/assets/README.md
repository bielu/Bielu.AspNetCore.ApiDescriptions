# @bielu/scalar-graph

Diagram primitives shared by the Bielu Scalar document-type plugins (`@bielu/scalar-arazzo`,
`@bielu/scalar-likec4`). Each plugin keeps its own model and layout (dagre for Arazzo, LikeC4's own export
for LikeC4); this package draws and handles what they have in common:

- **`geometry.ts`**: `splinePath` (a Graphviz Bézier route), `roundedPolylinePath` (a polyline with rounded
  corners), `arrowHead` / `edgeArrows`, `dashArray`. Points are `{ x, y }`.
- **`text.ts`**: `truncate` and `wrapText`, with widths estimated rather than measured, so a diagram draws the
  same on the page and in an exported file.
- **`pan-zoom.ts`**: pan and zoom state, zooming around a pivot, and the keyboard equivalents.
- **`export.ts`**: `standaloneSvg`, `svgToPng`, `downloadBlob`, `safeName`.

## This package is private

Like `@bielu/scalar-core`, it is **not published to npm**. The plugin packages depend on it with a `file:` link
in their `devDependencies`, and their Vite builds inline it. Unlike core it has no build step: it ships its
TypeScript source through `exports`, so it can hold Vue components later, which `tsc` cannot compile.

Do not re-export its types from a plugin package's public API: the published `dist/types` would then reference
a module consumers cannot resolve.
