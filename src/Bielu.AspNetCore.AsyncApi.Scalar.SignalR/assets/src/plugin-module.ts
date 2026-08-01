import { createPluginModule } from '@bielu/scalar-core'
import SignalRConsole from './components/SignalRConsole.vue'
import { SIGNALR_PLUGIN_SPEC } from './plugin'

/**
 * ESM entry point for Scalar's `pluginUrls` configuration (built as `dist/scalar-plugin.mjs`).
 *
 * Scalar `import()`s this module before mounting and registers the default export as a plugin, so
 * the console can be added to a stock Scalar bundle through JSON configuration alone — which is what
 * the Aspire AppHost needs, since the Scalar container's HTML is not ours to edit.
 *
 * The `<script>`-tag build (`dist/plugin.js`) remains the entry for the in-process ASP.NET Core
 * packages, which inject a script tag and hook `window.Scalar` instead.
 */
export default createPluginModule(
  { ...SIGNALR_PLUGIN_SPEC, stylesId: 'bielu-signalr-styles' },
  SignalRConsole,
  import.meta.url,
)
