import { registerConsoleElement } from '@bielu/scalar-core'
import LikeC4Document from './components/LikeC4Document.vue'
import { createLikeC4Plugin, LIKEC4_ELEMENT_SPEC } from './plugin'

/**
 * ESM entry point for Scalar's `pluginUrls` configuration (built as `dist/scalar-plugin.mjs`).
 *
 * Scalar `import()`s this module before mounting and registers the default export as a plugin, so
 * LikeC4 models can be added to a stock Scalar bundle through JSON configuration alone. The element
 * is registered here because there is no separate bootstrap step on this path.
 */
registerConsoleElement(LIKEC4_ELEMENT_SPEC, LikeC4Document)

export default createLikeC4Plugin()
