import type { SignalRDocumentRef } from './types'

/**
 * Derives candidate documents straight from the Scalar configuration's `sources` (and the legacy
 * top-level `url`/`content`). Scalar already knows every document, so the SignalR console does not
 * need them declared a second time — non-AsyncAPI sources are filtered out later.
 */
export function documentsFromScalarConfig(config: Record<string, any> | undefined): SignalRDocumentRef[] {
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
 * Resolves the documents to scan, in decreasing priority:
 *  - an explicit override (`documents` prop, or a `window.__BIELU_SCALAR_SIGNALR__` global);
 *  - otherwise auto-discovery from the Scalar configuration passed as `options`.
 */
export function resolveDocuments(
  options: Record<string, any> | undefined,
  override?: SignalRDocumentRef[],
): SignalRDocumentRef[] {
  if (override && override.length > 0) {
    return override
  }
  const injected = (globalThis as any).__BIELU_SCALAR_SIGNALR__?.documents as SignalRDocumentRef[] | undefined
  if (injected && injected.length > 0) {
    return injected
  }
  return documentsFromScalarConfig(options)
}
