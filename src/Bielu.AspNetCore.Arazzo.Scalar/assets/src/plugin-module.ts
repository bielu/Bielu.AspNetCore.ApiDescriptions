import { registerConsoleElement } from '@bielu/scalar-core'
import ArazzoDocument from './components/ArazzoDocument.vue'
import { ARAZZO_ELEMENT_SPEC, createArazzoPlugin } from './plugin'

/**
 * ESM entry point for Scalar's `pluginUrls` configuration (built as `dist/scalar-plugin.mjs`).
 *
 * Scalar `import()`s this module before mounting and registers the default export as a plugin, so
 * Arazzo documents can be added to a stock Scalar bundle through JSON configuration alone. The
 * element is registered here because there is no separate bootstrap step on this path.
 */
registerConsoleElement(ARAZZO_ELEMENT_SPEC, ArazzoDocument)

export default createArazzoPlugin()
