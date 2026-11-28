import { createDocumentTypePlugin, documentTypeOf } from '@bielu/scalar-core/document-type'
import { isLikeC4Model, traverseLikeC4Model } from './helpers/navigation'
import type { PluginNavigationEntry } from './types'

/** The custom element tag the document content is rendered as (registered by the entry points). */
export const LIKEC4_DOCUMENT_TAG = 'bielu-likec4-document'

/** The Scalar plugin's identifier, used to detect (and de-duplicate) an existing registration. */
export const LIKEC4_PLUGIN_NAME = 'bielu-likec4'

/** The `documentTypes[].type` claimed documents are tagged with. */
export const LIKEC4_DOCUMENT_TYPE = 'likec4'

/** How the element registers on the page (shared with `@bielu/scalar-core`'s element registration). */
export const LIKEC4_ELEMENT_SPEC = {
  pluginName: LIKEC4_PLUGIN_NAME,
  elementTag: LIKEC4_DOCUMENT_TAG,
  sidebarLabel: 'LikeC4',
  stylesId: 'bielu-likec4-styles',
} as const

/** A document type, structurally matching `PluginDocumentType` in `@scalar/types` (checked by `test/scalar-contract.ts`). */
export type LikeC4DocumentType = {
  type: string
  detect: (document: Record<string, unknown>) => boolean
  traverse: (context: {
    documentName: string
    documentId: string
    document: Record<string, unknown>
  }) => PluginNavigationEntry[]
  content: { component: unknown }
}

/**
 * The structural shape of a Scalar API Reference plugin. Declared locally so the published
 * declaration files never reference the private core package or a specific `@scalar/types`
 * version; Scalar validates it at runtime.
 */
export type ApiReferencePlugin = () => {
  name: string
  /** Always empty: the plugin contributes a document type, not OpenAPI extensions. */
  extensions: never[]
  documentTypes: LikeC4DocumentType[]
}

/** The LikeC4 document type: claims the JSON `likec4 export json` writes before the OpenAPI pipeline sees it. */
export const likeC4DocumentType: LikeC4DocumentType = documentTypeOf({
  type: LIKEC4_DOCUMENT_TYPE,
  elementTag: LIKEC4_DOCUMENT_TAG,
  detect: isLikeC4Model,
  traverse: traverseLikeC4Model,
})

/**
 * Creates the LikeC4 plugin for the Scalar API Reference. It needs `@scalar/api-reference` 1.80.0 or later
 * (plugin `documentTypes`, scalar/scalar#10816); older versions ignore the field, and would then render a
 * LikeC4 source as an empty OpenAPI document.
 */
export const createLikeC4Plugin = (): ApiReferencePlugin => createDocumentTypePlugin(LIKEC4_PLUGIN_NAME, likeC4DocumentType)
