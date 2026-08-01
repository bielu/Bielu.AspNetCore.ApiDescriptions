import { createPluginModule } from '@bielu/scalar-core'
import GrpcConsole from './components/GrpcConsole.vue'
import { setBundleScriptSrc } from './grpc-client'
import { GRPC_PLUGIN_SPEC } from './plugin'

/**
 * ESM entry point for Scalar's `pluginUrls` configuration (built as `dist/scalar-plugin.mjs`).
 *
 * Note the name: this package also ships a `dist/plugin.mjs`, which is the library entry built from
 * `index.ts` and self-installs by hooking `window.Scalar`. The two are not interchangeable.
 *
 * Scalar `import()`s this module before mounting and registers the default export as a plugin, so
 * the console can be added to a stock Scalar bundle through JSON configuration alone — which is what
 * the Aspire AppHost needs, since the Scalar container's HTML is not ours to edit.
 *
 * The `<script>`-tag build (`dist/plugin.js`) remains the entry for the in-process ASP.NET Core
 * package, which injects a script tag and hooks `window.Scalar` instead.
 */

// Remember where this module was loaded from — the protobuf descriptor endpoint is served as a
// sibling of the bundle. `document.currentScript` is null in an ES module, so use the module URL.
setBundleScriptSrc(import.meta.url)

export default createPluginModule(
  { ...GRPC_PLUGIN_SPEC, stylesId: 'bielu-grpc-styles' },
  GrpcConsole,
  import.meta.url,
)
