import { isArazzoDocument, traverseArazzoDocument } from './helpers/navigation'
import type { PluginNavigationEntry } from './types'

/** The custom element tag the document content is rendered as (registered by the entry points). */
export const ARAZZO_DOCUMENT_TAG = 'bielu-arazzo-document'

/** The Scalar plugin's identifier, used to detect (and de-duplicate) an existing registration. */
export const ARAZZO_PLUGIN_NAME = 'bielu-arazzo'

/** The `documentTypes[].type` claimed documents are tagged with. */
export const ARAZZO_DOCUMENT_TYPE = 'arazzo'

/** How the element registers on the page (shared with `@bielu/scalar-core`'s element registration). */
export const ARAZZO_ELEMENT_SPEC = {
  pluginName: ARAZZO_PLUGIN_NAME,
  elementTag: ARAZZO_DOCUMENT_TAG,
  sidebarLabel: 'Arazzo',
  stylesId: 'bielu-arazzo-styles',
} as const

/** A document type, structurally matching `PluginDocumentType` in `@scalar/types` (scalar/scalar#10816). */
export type ArazzoDocumentType = {
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
  extensions: unknown[]
  documentTypes: ArazzoDocumentType[]
}

/** The Arazzo document type: claims `arazzo: "1.x"` documents before the OpenAPI pipeline sees them. */
export const arazzoDocumentType: ArazzoDocumentType = {
  type: ARAZZO_DOCUMENT_TYPE,
  detect: isArazzoDocument,
  traverse: traverseArazzoDocument,
  // A tag name, so Scalar's own Vue renders a plain element and our Web Component (with its own
  // Vue) takes over; Scalar sets the content props on it as DOM properties.
  content: { component: ARAZZO_DOCUMENT_TAG },
}

/**
 * Creates the Arazzo plugin for the Scalar API Reference. It needs a Scalar version with plugin
 * `documentTypes` (scalar/scalar#10816); older versions ignore the field, and would then render an
 * Arazzo source as an empty OpenAPI document.
 */
export const createArazzoPlugin = (): ApiReferencePlugin => {
  const plugin: ApiReferencePlugin = () => ({
    name: ARAZZO_PLUGIN_NAME,
    extensions: [],
    documentTypes: [arazzoDocumentType],
  })
  // Tag the factory so a page-level hook can detect an already-registered plugin without invoking it.
  ;(plugin as { pluginName?: string }).pluginName = ARAZZO_PLUGIN_NAME
  return plugin
}
