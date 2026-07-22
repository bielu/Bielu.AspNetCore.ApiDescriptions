import type { DocumentRef, SecuritySchemeModel } from './types'

// Cap each AsyncAPI document fetch so a hung endpoint can't keep a console panel loading forever.
const DOC_FETCH_TIMEOUT_MS = 10000

type AnyRecord = Record<string, any>

/** Resolve a local JSON pointer (`#/a/b/c`) against the document. */
export function pointer(doc: AnyRecord, ref: string): AnyRecord | undefined {
  if (typeof ref !== 'string' || !ref.startsWith('#/')) {
    return undefined
  }
  let node: any = doc
  for (const raw of ref.slice(2).split('/')) {
    if (node == null) {
      return undefined
    }
    node = node[raw.replace(/~1/g, '/').replace(/~0/g, '~')]
  }
  return node
}

/** Follow a chain of `$ref`s until a concrete node is reached (cycle-safe). */
export function deref(doc: AnyRecord, node: AnyRecord | undefined, seen = new Set<string>()): AnyRecord | undefined {
  let current = node
  while (current && typeof current.$ref === 'string') {
    if (seen.has(current.$ref)) {
      return undefined
    }
    seen.add(current.$ref)
    current = pointer(doc, current.$ref)
  }
  return current
}

/** The last segment of a JSON pointer / `$ref` (e.g. `#/channels/chat` → `chat`). */
export function refName(ref: string | undefined): string | undefined {
  if (!ref) {
    return undefined
  }
  const parts = ref.split('/')
  return parts[parts.length - 1]
}

function pageScheme(): string {
  if (typeof window !== 'undefined' && window.location?.protocol) {
    return window.location.protocol.replace(':', '')
  }
  return 'http'
}

/**
 * Build an absolute base URL (`scheme://host`) from an AsyncAPI server host string. Accepts bare
 * hosts (which inherit the page scheme) as well as `http`/`https`/`ws`/`wss` URLs; the WebSocket
 * schemes are mapped onto HTTP(S) (browser clients expect an HTTP endpoint) and any path/query is
 * stripped so only `scheme://host` remains.
 */
export function resolveServerBaseUrl(host: string | undefined): string {
  if (!host) {
    return typeof window !== 'undefined' ? window.location.origin : ''
  }
  const trimmed = host.trim()
  const withScheme = /^[a-z][a-z0-9+.-]*:\/\//i.test(trimmed) ? trimmed : `${pageScheme()}://${trimmed}`
  try {
    const url = new URL(withScheme)
    const protocol = url.protocol === 'wss:' ? 'https:' : url.protocol === 'ws:' ? 'http:' : url.protocol
    return `${protocol}//${url.host}`
  } catch {
    // Best-effort fallback for an unparseable host: keep the previous behaviour,
    // but without using a regex that might be flagged as polynomial.
    let result = trimmed;
    while (result.endsWith('/')) {
      result = result.slice(0, -1);
    }
    return `${pageScheme()}://${result}`;
  }
}

/** The host of the first AsyncAPI server that speaks the given protocol (or carries its binding). */
export function firstServerHost(doc: AnyRecord, protocol: string): string | undefined {
  const servers: AnyRecord = doc.servers ?? {}
  for (const key of Object.keys(servers)) {
    const server: AnyRecord = servers[key] ?? {}
    if (server.protocol === protocol || server.bindings?.[protocol]) {
      return server.host ?? server.url
    }
  }
  return undefined
}

/**
 * Extract the document's security schemes in the minimal shape needed for auth resolution, so a
 * console can read them at connect time without re-fetching the document.
 */
export function extractSecuritySchemes(doc: AnyRecord): Record<string, SecuritySchemeModel> {
  const rawSchemes: AnyRecord = doc.components?.securitySchemes ?? {}
  const securitySchemes: Record<string, SecuritySchemeModel> = {}
  for (const [key, raw] of Object.entries(rawSchemes)) {
    if (raw && typeof raw === 'object') {
      const s = raw as AnyRecord
      securitySchemes[key] = {
        type: typeof s.type === 'string' ? s.type : '',
        ...(typeof s.in === 'string' ? { in: s.in } : {}),
        ...(typeof s.name === 'string' ? { name: s.name } : {}),
        ...(typeof s.scheme === 'string' ? { scheme: s.scheme } : {}),
      }
    }
  }
  return securitySchemes
}

/**
 * Resolve a document URL the way Scalar does. Scalar source URLs are app-root-relative with the
 * leading slash stripped (e.g. `asyncapi/signalr.json`), so they must resolve against the origin —
 * not the current page path (`/scalar/...`), which would 404.
 */
function resolveDocUrl(url: string): string {
  if (typeof window === 'undefined' || !window.location?.origin) {
    return url
  }
  try {
    return new URL(url, window.location.origin).href
  } catch {
    return url
  }
}

/** A named, parsed AsyncAPI document. */
export type LoadedDocument = { name: string; doc: AnyRecord }

/**
 * Resolve the configured documents (inline object or URL) to parsed AsyncAPI documents. Only
 * documents that are actually AsyncAPI (have an `asyncapi` version field) are returned, so OpenAPI
 * sources in the same Scalar configuration are ignored. Individual document failures (including
 * fetch timeouts) are skipped so a console still renders the rest.
 */
export async function loadAsyncApiDocuments(documents: DocumentRef[]): Promise<LoadedDocument[]> {
  const all: LoadedDocument[] = []
  for (const ref of documents) {
    try {
      let doc = ref.doc
      if (!doc && ref.url) {
        const controller = new AbortController()
        const timer = setTimeout(() => controller.abort(), DOC_FETCH_TIMEOUT_MS)
        try {
          const response = await fetch(resolveDocUrl(ref.url), {
            headers: { accept: 'application/json' },
            signal: controller.signal,
          })
          if (!response.ok) {
            continue
          }
          doc = await response.json()
        } finally {
          clearTimeout(timer)
        }
      }
      if (!doc || !doc.asyncapi) {
        continue
      }
      all.push({ name: ref.name, doc })
    } catch {
      // Ignore individual document failures so the console still renders the rest.
    }
  }
  return all
}
