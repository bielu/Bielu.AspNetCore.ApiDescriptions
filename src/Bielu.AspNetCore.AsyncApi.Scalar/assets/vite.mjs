// @ts-check
import { fileURLToPath } from 'node:url'

/**
 * The three bundles every document-type plugin package (`@bielu/scalar-arazzo`, `@bielu/scalar-likec4`) builds
 * from one `vite.config.ts`, picked by `vite build --mode <target>`:
 *
 * - `iife` (the default mode, `production`): `dist/plugin.js`, a script that registers the document element and hooks
 *   `window.Scalar.createApiReference`. It does not bundle `@scalar/api-reference`: the page still loads Scalar's own bundle.
 * - `module`: `dist/scalar-plugin.mjs`, the same contents as an ES module whose default export is the plugin, because
 *   Scalar's `pluginUrls` loads each entry with a dynamic `import()` and registers `module.default`.
 * - `library`: `dist/index.mjs`, the package root with its named exports and no hook on `window.Scalar`: whoever imports
 *   the package adds the plugin themselves.
 *
 * Only the IIFE build empties `dist`; the other two run after it and must not delete `dist/plugin.js`.
 */
const TARGETS = {
  iife: { entry: 'src/script.ts', formats: ['iife'], fileName: 'plugin.js', emptyOutDir: true },
  module: { entry: 'src/plugin-module.ts', formats: ['es'], fileName: 'scalar-plugin.mjs', emptyOutDir: false },
  library: { entry: 'src/index.ts', formats: ['es'], fileName: 'index.mjs', emptyOutDir: false },
}

/**
 * Returns the Vite config for one of a plugin package's bundles. It imports no Vite plugin itself: the caller passes
 * them (`vue({ customElement: true })`, `tailwindcss()`), so they resolve from the plugin package, not from this folder.
 *
 * @param {{ root: URL, globalName: string, mode: string, plugins: unknown[] }} options `root` is the package folder
 *   (`new URL('.', import.meta.url)` in its `vite.config.ts`), `globalName` the IIFE's global, `mode` Vite's `--mode`
 *   (`module`, `library`, anything else builds the IIFE; `vitest` passes `test`), and `plugins` the Vite plugins.
 * @returns {Record<string, any>} a plain config, for `defineConfig`.
 */
export function scalarPluginBuild({ root, globalName, mode, plugins }) {
  const target = TARGETS[/** @type {keyof typeof TARGETS} */ (mode)] ?? TARGETS.iife
  return {
    // `customElement: true` compiles the SFCs' styles to strings the element registration injects.
    // `tailwindcss()` compiles src/style.css (on Scalar's theme, see @bielu/scalar-core's tailwind.css).
    plugins,
    resolve: {
      alias: {
        '@': fileURLToPath(new URL('./src', root)),
      },
    },
    // Vue reads `process.env.NODE_ENV`; there is no `process` global in the browser.
    define: {
      'process.env.NODE_ENV': JSON.stringify('production'),
    },
    build: {
      outDir: 'dist',
      emptyOutDir: target.emptyOutDir,
      lib: {
        entry: fileURLToPath(new URL(target.entry, root)),
        ...(target.formats[0] === 'iife' ? { name: globalName } : {}),
        formats: target.formats,
        fileName: () => target.fileName,
      },
      rollupOptions: {
        output: {
          inlineDynamicImports: true,
        },
      },
    },
  }
}
