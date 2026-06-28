import type { SignalRDirection, SignalRDocumentRef, SignalRHubModel, SignalROperationModel } from './types'

const SIGNALR = 'signalr'

type AnyRecord = Record<string, any>

function pageScheme(): string {
  if (typeof window !== 'undefined' && window.location?.protocol) {
    return window.location.protocol.replace(':', '')
  }
  return 'http'
}

/** Build an absolute base URL (`scheme://host`) from an AsyncAPI server host string. */
function resolveServerBaseUrl(host: string | undefined): string {
  if (!host) {
    return typeof window !== 'undefined' ? window.location.origin : ''
  }
  if (/^https?:\/\//i.test(host)) {
    return host.replace(/\/+$/, '')
  }
  return `${pageScheme()}://${host.replace(/\/+$/, '')}`
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
    }
    hub.operations.push(model)
  }

  return [...hubs.values()]
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
        const response = await fetch(ref.url, { headers: { accept: 'application/json' } })
        if (!response.ok) {
          continue
        }
        doc = await response.json()
      }
      if (!doc || !doc.asyncapi) {
        continue
      }
      all.push(...parseSignalRHubs(ref.name, doc))
    } catch {
      // Ignore individual document failures so the console still renders the rest.
    }
  }
  return all
}
