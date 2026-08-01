import type { DiscoveryOptions, DocumentRef } from './types'

/**
 * Minimal replica of Scalar's `slugify` + `slugger` pipeline from
 * `@scalar/helpers/string/slugify` + `@scalar/helpers/string/slugger`.
 *
 * Scalar stores each AsyncAPI document in its workspace under the key
 * `slugger(source.title)` — i.e. the lowercased, non-word-stripped,
 * space-collapsed version of the display title. The auth store is keyed by
 * the same string, so a console's `documentName` must match it exactly.
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
 * top-level `url`/`content`). Scalar already knows every document, so a console does not need them
 * declared a second time — non-AsyncAPI sources are filtered out later.
 *
 * The `name` on each ref is set to `scalarSlugify(source.title)` so it matches the key that
 * Scalar's workspace store (and auth store) uses for the document — which is derived the same way
 * in `normalizeConfigurations` via `slug(source.title)`.
 */
export function documentsFromScalarConfig(config: Record<string, any> | undefined): DocumentRef[] {
  if (!config) {
    return []
  }
  const refs: DocumentRef[] = []
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

/**
 * Type guard: a non-empty array of well-formed `DocumentRef`s (each with a `name` and a `url` or `doc`).
 *
 * `doc` is checked to be a non-array object, not merely non-null: `DocumentRef.doc` is typed
 * `Record<string, any>`, so admitting `doc: 1` would hand downstream code a value the type says it
 * cannot receive. That matters because this guard is the only validation applied to untrusted input —
 * the base64 payload the .NET Aspire packages put on the plugin module's URL.
 */
export function isDocumentRefArray(value: unknown): value is DocumentRef[] {
  // `null` is treated as absent rather than as a malformed value, so a JSON payload that spells an
  // omitted field as `null` is still accepted.
  const isPresent = (field: unknown): boolean => field !== undefined && field !== null
  const isDocumentObject = (doc: unknown): doc is Record<string, any> =>
    typeof doc === 'object' && doc !== null && !Array.isArray(doc)

  return (
    Array.isArray(value) &&
    value.length > 0 &&
    value.every((entry): entry is DocumentRef => {
      if (entry === null || typeof entry !== 'object') {
        return false
      }

      const { name, url, doc } = entry as DocumentRef
      if (typeof name !== 'string') {
        return false
      }

      // Every source that is present is validated on its own. Checking them with `||` would let a
      // valid `url` short-circuit the `doc` check, admitting `{ url: 'ok', doc: 1 }` — and `doc` is
      // the field consumers reach for first.
      const hasUrl = isPresent(url)
      const hasDoc = isPresent(doc)
      if (hasUrl && typeof url !== 'string') {
        return false
      }
      if (hasDoc && !isDocumentObject(doc)) {
        return false
      }

      // At least one usable source. Carrying both is legal, not an error: `loadAsyncApiDocuments`
      // prefers `doc` and only fetches `url` when `doc` is absent.
      return hasUrl || hasDoc
    })
  )
}

/**
 * Resolves the documents to scan, in decreasing priority:
 *  - an explicit override (`documents` prop);
 *  - a plugin config embedded inline on the Scalar config under `discovery.configKey`;
 *  - a `window[discovery.globalName]` global (injected by the .NET package);
 *  - otherwise auto-discovery from the Scalar configuration passed as `options`.
 *
 * The inline-config and global values are untyped input, so they are validated before use and
 * malformed values fall through to the next discovery source.
 */
export function resolveDocuments(
  options: Record<string, any> | undefined,
  discovery: DiscoveryOptions,
  override?: DocumentRef[],
): DocumentRef[] {
  if (override && override.length > 0) {
    return override
  }
  const fromConfig = options?.[discovery.configKey]?.documents
  if (isDocumentRefArray(fromConfig)) {
    return fromConfig
  }
  const injected = (globalThis as any)[discovery.globalName]?.documents
  if (isDocumentRefArray(injected)) {
    return injected
  }
  return documentsFromScalarConfig(options)
}
