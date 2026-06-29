import type {
  SignalRDirection,
  SignalRDocumentRef,
  SignalRHubModel,
  SignalRMessageModel,
  SignalROperationModel,
} from './types'

const SIGNALR = 'signalr'
const MAX_SCHEMA_DEPTH = 6
// Cap each AsyncAPI document fetch so a hung endpoint can't keep the SignalR panel loading forever.
const DOC_FETCH_TIMEOUT_MS = 10000

type AnyRecord = Record<string, any>

/** Resolve a local JSON pointer (`#/a/b/c`) against the document. */
function pointer(doc: AnyRecord, ref: string): AnyRecord | undefined {
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
function deref(doc: AnyRecord, node: AnyRecord | undefined, seen = new Set<string>()): AnyRecord | undefined {
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

// Numeric `format`s that some schema generators emit with `type: "string"` (e.g. .NET renders
// `int` as `{ type: "string", format: "int32" }`). Treated as numbers so examples stay invocable.
const NUMERIC_FORMATS = new Set(['int32', 'int64', 'integer', 'long', 'double', 'float', 'decimal', 'number'])

/** A placeholder value for a `string` schema, honouring common formats. */
function exampleString(schema: AnyRecord): string {
  switch (schema.format) {
    case 'date-time':
      return new Date().toISOString()
    case 'date':
      return new Date().toISOString().slice(0, 10)
    case 'time':
      return new Date().toISOString().slice(11, 19)
    case 'uuid':
    case 'guid':
      return '00000000-0000-0000-0000-000000000000'
    case 'email':
      return 'user@example.com'
    case 'uri':
    case 'url':
      return 'https://example.com'
    default:
      return 'string'
  }
}

/** Build a representative example value from a JSON schema node. */
function exampleFromSchema(doc: AnyRecord, node: AnyRecord | undefined, depth = 0, seen = new Set<string>()): unknown {
  const schema = deref(doc, node, new Set(seen))
  if (!schema || depth > MAX_SCHEMA_DEPTH) {
    return null
  }

  // AsyncAPI multi-format payloads wrap the JSON schema under `schema`.
  if (schema.schema && !schema.type && !schema.properties && !schema.enum) {
    return exampleFromSchema(doc, schema.schema, depth, seen)
  }

  // Prefer explicit, document-provided values.
  if (Array.isArray(schema.examples) && schema.examples.length > 0) {
    return schema.examples[0]
  }
  if (schema.example !== undefined) {
    return schema.example
  }
  if (schema.default !== undefined) {
    return schema.default
  }
  if (Array.isArray(schema.enum) && schema.enum.length > 0) {
    return schema.enum[0]
  }
  if (schema.const !== undefined) {
    return schema.const
  }

  const composite: AnyRecord[] | undefined = schema.allOf ?? schema.oneOf ?? schema.anyOf
  if (Array.isArray(composite) && composite.length > 0) {
    if (schema.allOf) {
      const merged: AnyRecord = {}
      for (const sub of composite) {
        const value = exampleFromSchema(doc, sub, depth, seen)
        if (value && typeof value === 'object' && !Array.isArray(value)) {
          Object.assign(merged, value)
        }
      }
      if (Object.keys(merged).length > 0) {
        return merged
      }
    }
    return exampleFromSchema(doc, composite[0], depth, seen)
  }

  const type = Array.isArray(schema.type) ? schema.type.find((t: string) => t !== 'null') : schema.type

  // A numeric format wins over a (sometimes incorrect) `string` type.
  if (typeof schema.format === 'string' && NUMERIC_FORMATS.has(schema.format)) {
    return typeof schema.minimum === 'number' ? schema.minimum : 0
  }

  switch (type) {
    case 'object':
      return exampleObject(doc, schema, depth, seen)
    case 'array': {
      const item = exampleFromSchema(doc, schema.items, depth + 1, seen)
      return item === null ? [] : [item]
    }
    case 'string':
      return exampleString(schema)
    case 'integer':
    case 'number':
      return typeof schema.minimum === 'number' ? schema.minimum : 0
    case 'boolean':
      return false
    case 'null':
      return null
    default:
      return schema.properties ? exampleObject(doc, schema, depth, seen) : null
  }
}

function exampleObject(doc: AnyRecord, schema: AnyRecord, depth: number, seen: Set<string>): AnyRecord {
  const out: AnyRecord = {}
  const properties: AnyRecord = schema.properties ?? {}
  for (const key of Object.keys(properties)) {
    out[key] = exampleFromSchema(doc, properties[key], depth + 1, seen)
  }
  return out
}

/** Resolve the message(s) an operation carries and generate an example payload for each. */
function buildMessages(doc: AnyRecord, op: AnyRecord): SignalRMessageModel[] {
  const refs = (Array.isArray(op.messages) ? op.messages : [])
    .map((m: AnyRecord) => m?.$ref)
    .filter((ref: unknown): ref is string => typeof ref === 'string')

  const messages: SignalRMessageModel[] = []
  for (const ref of refs) {
    const message = deref(doc, { $ref: ref })
    const name = message?.name ?? refName(ref) ?? 'message'
    const payloadExample = exampleFromSchema(doc, message?.payload)
    // SignalR hub methods take positional arguments; the payload is the (first) argument.
    const args = payloadExample === null ? [] : [payloadExample]
    messages.push({ name, title: message?.title, example: JSON.stringify(args, null, 2) })
  }
  return messages
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
 * schemes are mapped onto HTTP(S) (SignalR's `withUrl` expects an HTTP endpoint) and any path/query
 * is stripped so only `scheme://host` remains.
 */
function resolveServerBaseUrl(host: string | undefined): string {
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
    // Best-effort fallback for an unparseable host: keep the previous behaviour.
    return `${pageScheme()}://${trimmed.replace(/\/+$/, '')}`
  }
}

function firstSignalRServerHost(doc: AnyRecord): string | undefined {
  const servers: AnyRecord = doc.servers ?? {}
  for (const key of Object.keys(servers)) {
    const server: AnyRecord = servers[key] ?? {}
    if (server.protocol === SIGNALR || server.bindings?.[SIGNALR]) {
      return server.host ?? server.url
    }
  }
  return undefined
}

function refName(ref: string | undefined): string | undefined {
  if (!ref) {
    return undefined
  }
  const parts = ref.split('/')
  return parts[parts.length - 1]
}

function directionFor(op: AnyRecord, binding: AnyRecord | undefined): SignalRDirection {
  const fromBinding = binding?.direction
  if (fromBinding === 'clientToServer' || fromBinding === 'serverToClient') {
    return fromBinding
  }
  // AsyncAPI 3 `action`: `send` = application -> channel, `receive` = channel -> application.
  return op.action === 'receive' ? 'serverToClient' : 'clientToServer'
}

/** Extract every SignalR hub (channel + its operations) from a parsed AsyncAPI document. */
export function parseSignalRHubs(documentName: string, doc: AnyRecord): SignalRHubModel[] {
  if (!doc || typeof doc !== 'object') {
    return []
  }

  const baseUrl = resolveServerBaseUrl(firstSignalRServerHost(doc))
  const channels: AnyRecord = doc.channels ?? {}
  const hubs = new Map<string, SignalRHubModel>()

  for (const channelName of Object.keys(channels)) {
    const channel: AnyRecord = channels[channelName] ?? {}
    const binding: AnyRecord | undefined = channel.bindings?.[SIGNALR]
    if (!binding) {
      continue
    }
    hubs.set(channelName, {
      documentName,
      channelName,
      hubPath: binding.hub ?? channel.address ?? `/${channelName}`,
      baseUrl,
      transports: binding.transports ?? [],
      protocols: binding.protocols ?? [],
      operations: [],
    })
  }

  const operations: AnyRecord = doc.operations ?? {}
  for (const opName of Object.keys(operations)) {
    const op: AnyRecord = operations[opName] ?? {}
    const channelName = refName(op.channel?.$ref)
    const hub = channelName ? hubs.get(channelName) : undefined
    if (!hub) {
      continue
    }
    const binding: AnyRecord | undefined = op.bindings?.[SIGNALR]
    const model: SignalROperationModel = {
      id: opName,
      target: binding?.target ?? opName,
      direction: directionFor(op, binding),
      callType: binding?.callType,
      summary: op.summary ?? op.description,
      messages: buildMessages(doc, op),
    }
    hub.operations.push(model)
  }

  return [...hubs.values()]
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

/**
 * Resolve the configured documents (inline object or URL) and aggregate the SignalR hubs they
 * declare. Only documents that are actually AsyncAPI (have an `asyncapi` version field) are
 * considered, so OpenAPI sources in the same Scalar configuration are ignored.
 */
export async function loadSignalRHubs(documents: SignalRDocumentRef[]): Promise<SignalRHubModel[]> {
  const all: SignalRHubModel[] = []
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
      all.push(...parseSignalRHubs(ref.name, doc))
    } catch {
      // Ignore individual document failures (including fetch timeouts) so the console still renders the rest.
    }
  }
  return all
}
