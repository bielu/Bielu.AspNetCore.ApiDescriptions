import { createApiReference as createScalarApiReference } from '@scalar/api-reference'
import { createSignalRPlugin } from './plugin'
import type { SignalRDocumentRef, SignalRPluginConfig } from './types'

export { createSignalRPlugin } from './plugin'
export { loadSignalRHubs, parseSignalRHubs } from './signalr-bindings'
export type {
  SignalRDirection,
  SignalRDocumentRef,
  SignalRHubModel,
  SignalROperationModel,
  SignalRPluginConfig,
} from './types'

/**
 * Config captured from this bundle's own `<script>` URL, e.g.
 * `bundle.js?documents=<base64-json>`. This is the channel used by the Aspire helper, where the
 * Scalar container HTML cannot inject a `HeadContent` global. Must be read while the IIFE is
 * executing (that is the only moment `document.currentScript` is valid), so it runs at module load.
 */
const scriptConfig: Partial<SignalRPluginConfig> = (() => {
  if (typeof document === 'undefined') {
    return {}
  }
  const src = (document.currentScript as HTMLScriptElement | null)?.src
  if (!src) {
    return {}
  }
  try {
    const raw = new URL(src).searchParams.get('documents')
    return raw ? { documents: JSON.parse(atob(raw)) } : {}
  } catch {
    return {}
  }
})()

/**
 * Derives the candidate documents straight from the Scalar configuration's `sources` (and the
 * legacy top-level `url`/`content`). Scalar already knows every document, so the SignalR console
 * does not need them declared a second time — non-AsyncAPI sources are filtered out later.
 */
function documentsFromScalarConfig(config: Record<string, any> | undefined): SignalRDocumentRef[] {
  if (!config) {
    return []
  }
  const refs: SignalRDocumentRef[] = []
  const seen = new Set<string>()

  const addUrl = (url: unknown, title?: unknown) => {
    if (typeof url === 'string' && url && !seen.has(url)) {
      seen.add(url)
      refs.push({ name: typeof title === 'string' && title ? title : url, url })
    }
  }
  const addContent = (content: unknown, title?: unknown) => {
    if (content && typeof content === 'object') {
      refs.push({ name: typeof title === 'string' && title ? title : 'document', doc: content as Record<string, any> })
    }
  }

  const sources = Array.isArray(config.sources) ? config.sources : []
  for (const source of sources) {
    addUrl(source?.url ?? source?.spec?.url, source?.title ?? source?.slug)
    addContent(source?.content ?? source?.spec?.content, source?.title ?? source?.slug)
  }
  addUrl(config.url)
  addContent(config.content)

  return refs
}

/**
 * Resolves the SignalR config. The document list is, in decreasing priority:
 *  - an explicit override (inline `config.signalr`, the injected global, or the `<script>` query);
 *  - otherwise auto-discovered from the Scalar configuration's `sources`.
 */
function resolveSignalRConfig(config: Record<string, any> | undefined): SignalRPluginConfig {
  const injected = (globalThis as any).__BIELU_SCALAR_SIGNALR__ as Partial<SignalRPluginConfig> | undefined
  const inline = config?.signalr as Partial<SignalRPluginConfig> | undefined
  const override = inline?.documents ?? injected?.documents ?? scriptConfig.documents
  return {
    documents: override && override.length > 0 ? override : documentsFromScalarConfig(config),
  }
}

/**
 * Drop-in replacement for `Scalar.createApiReference` that additionally registers the interactive
 * SignalR console plugin.
 */
export function createApiReference(element: unknown, config: Record<string, any> = {}) {
  const signalr = resolveSignalRConfig(config)
  const plugins = [...(config.plugins ?? []), createSignalRPlugin(signalr)]
  return createScalarApiReference(element as any, { ...config, plugins })
}

// Expose the wrapper on the global so the standard Scalar HTML shell — which calls
// `Scalar.createApiReference(...)` — picks up the SignalR-enabled version unchanged.
if (typeof window !== 'undefined') {
  const existing = (window as any).Scalar ?? {}
  ;(window as any).Scalar = { ...existing, createApiReference }
}
