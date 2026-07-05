import type { SignalRDocumentRef } from './types'

/**
 * Minimal replica of Scalar's `slugify` + `slugger` pipeline from
 * `@scalar/helpers/string/slugify` + `@scalar/helpers/string/slugger`.
 *
 * Scalar stores each AsyncAPI document in its workspace under the key
 * `slugger(source.title)` — i.e. the lowercased, non-word-stripped,
 * space-collapsed version of the display title. The auth store is keyed by
 * the same string, so our `hub.documentName` must match it exactly.
 *
 * We replicate only the subset we need (no options, no deduplication suffix)
 * to avoid a hard dependency on `@scalar/helpers`.
 */
function scalarSlugify(v: string): string {
  return v
    .slice(0, 255)
    .trim()
    .normalize('NFC')
    .toLowerCase()
    .replace(/[^\p{L}\p{M}\p{N}\s_-]/gu, '') // strip chars that aren't letters/marks/digits/spaces/–
    .replace(/[\s_-]+/g, '-') // collapse runs of whitespace/underscores/hyphens
    .replace(/^-+|-+$/g, '') // trim leading/trailing hyphens
}

/**
 * Derives candidate documents straight from the Scalar configuration's `sources` (and the legacy
 * top-level `url`/`content`). Scalar already knows every document, so the SignalR console does not
 * need them declared a second time — non-AsyncAPI sources are filtered out later.
 *
 * The `name` on each ref is set to `scalarSlugify(source.title)` so it matches the key that
 * Scalar's workspace store (and auth store) uses for the document — which is derived the same way
 * in `normalizeConfigurations` via `slug(source.title)`.
 */
export function documentsFromScalarConfig(config: Record<string, any> | undefined): SignalRDocumentRef[] {
  if (!config) {
    return []
  }
  const refs: SignalRDocumentRef[] = []
  const seen = new Set<string>()

  const nameFor = (title: unknown, fallback: string): string => {
    const t = typeof title === 'string' && title ? title : null
    return t ? scalarSlugify(t) : fallback
  }

  const addUrl = (url: unknown, title?: unknown) => {
    if (typeof url === 'string' && url && !seen.has(url)) {
      seen.add(url)
      refs.push({ name: nameFor(title, url), url })
    }
  }
  const addContent = (content: unknown, title?: unknown) => {
    if (content && typeof content === 'object') {
      refs.push({ name: nameFor(title, 'document'), doc: content as Record<string, any> })
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

/** Type guard: a non-empty array of well-formed `SignalRDocumentRef`s (each with a `name` and a `url` or `doc`). */
function isDocumentRefArray(value: unknown): value is SignalRDocumentRef[] {
  return (
    Array.isArray(value) &&
    value.length > 0 &&
    value.every(
      (entry): entry is SignalRDocumentRef =>
        entry != null &&
        typeof entry === 'object' &&
        typeof (entry as SignalRDocumentRef).name === 'string' &&
        (typeof (entry as SignalRDocumentRef).url === 'string' || (entry as SignalRDocumentRef).doc != null),
    )
  )
}

/**
 * Resolves the documents to scan, in decreasing priority:
 *  - an explicit override (`documents` prop);
 *  - a `SignalRPluginConfig` embedded inline on the Scalar config as `config.signalr`;
 *  - a `window.__BIELU_SCALAR_SIGNALR__` global;
 *  - otherwise auto-discovery from the Scalar configuration passed as `options`.
 *
 * The `config.signalr` and global values are untyped input, so they are validated before use and
 * malformed values fall through to the next discovery source.
 */
export function resolveDocuments(
  options: Record<string, any> | undefined,
  override?: SignalRDocumentRef[],
): SignalRDocumentRef[] {
  if (override && override.length > 0) {
    return override
  }
  const fromConfig = options?.signalr?.documents
  if (isDocumentRefArray(fromConfig)) {
    return fromConfig
  }
  const injected = (globalThis as any).__BIELU_SCALAR_SIGNALR__?.documents
  if (isDocumentRefArray(injected)) {
    return injected
  }
  return documentsFromScalarConfig(options)
}
