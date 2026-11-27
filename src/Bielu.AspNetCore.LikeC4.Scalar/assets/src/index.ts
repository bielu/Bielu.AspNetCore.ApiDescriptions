import { registerConsoleElement } from '@bielu/scalar-core'
import LikeC4Document from './components/LikeC4Document.vue'
import { LIKEC4_ELEMENT_SPEC } from './plugin'

export { isLikeC4Model, traverseLikeC4Model } from './helpers/navigation'
export { createLikeC4Plugin, likeC4DocumentType } from './plugin'
export type { ApiReferencePlugin, LikeC4DocumentType } from './plugin'
export type { LikeC4Edge, LikeC4Model, LikeC4Node, LikeC4View, PluginNavigationEntry } from './types'

// The package root (`import { createLikeC4Plugin } from '@bielu/scalar-likec4'`, built as `dist/index.mjs`):
// register the element the document type renders, and leave adding the plugin to the caller. The
// `<script>`-tag entry (src/script.ts) also hooks `window.Scalar.createApiReference`.
registerConsoleElement(LIKEC4_ELEMENT_SPEC, LikeC4Document)
