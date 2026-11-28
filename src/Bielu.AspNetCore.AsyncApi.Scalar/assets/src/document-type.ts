import { defineComponent, h } from 'vue'

/**
 * A document type, structurally matching `PluginDocumentType` in `@scalar/types`. Generic over the navigation
 * entry, so each plugin package declares its own entry type and its published declarations never reference this
 * private package.
 */
export type DocumentTypeDefinition<TEntry> = {
  type: string
  detect: (document: Record<string, unknown>) => boolean
  traverse: (context: {
    documentName: string
    documentId: string
    document: Record<string, unknown>
  }) => TEntry[]
  content: { component: unknown }
}

/** What a document-type plugin declares about itself. */
export type DocumentTypeSpec<TEntry> = Omit<DocumentTypeDefinition<TEntry>, 'content'> & {
  /** The custom element tag the document content is rendered as. */
  elementTag: string
}

/** The structural shape of a Scalar API Reference plugin contributing one document type. */
export type DocumentTypePlugin<TEntry> = () => {
  name: string
  /** Always empty: the plugin contributes a document type, not OpenAPI extensions. */
  extensions: never[]
  documentTypes: DocumentTypeDefinition<TEntry>[]
}

/**
 * The document type whose content is the plugin's custom element. A tag name, so Scalar's own Vue renders a plain
 * element and the plugin's Web Component (with its own Vue) takes over; Scalar sets the content props on it as DOM
 * properties.
 */
export const documentTypeOf = <TEntry>({ elementTag, ...rest }: DocumentTypeSpec<TEntry>): DocumentTypeDefinition<TEntry> => ({
  ...rest,
  content: { component: elementTag },
})

/**
 * Creates a Scalar plugin factory that contributes `documentType`, tagged with `pluginName` so a page-level hook can
 * detect an already-registered plugin without invoking it.
 *
 * With `contentProps`, the content is a Vue component that renders `elementTag` with Scalar's props plus these, for
 * options resolved when the factory is created.
 */
export const createDocumentTypePlugin = <TEntry>(
  pluginName: string,
  documentType: DocumentTypeDefinition<TEntry>,
  contentProps?: { elementTag: string; props: Record<string, unknown> },
): DocumentTypePlugin<TEntry> => {
  const plugin: DocumentTypePlugin<TEntry> = () => ({
    name: pluginName,
    extensions: [],
    documentTypes: [contentProps ? { ...documentType, content: { component: wrap(contentProps.elementTag, contentProps.props) } } : documentType],
  })
  ;(plugin as { pluginName?: string }).pluginName = pluginName
  return plugin
}

const wrap = (elementTag: string, props: Record<string, unknown>) =>
  defineComponent({
    inheritAttrs: false,
    setup(_, { attrs }) {
      return () => h(elementTag, { ...attrs, ...props })
    },
  })
