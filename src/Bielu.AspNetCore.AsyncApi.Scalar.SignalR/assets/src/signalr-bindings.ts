import {
  deref,
  exampleFromSchema,
  extractSecuritySchemes,
  firstServerHost,
  loadAsyncApiDocuments,
  refName,
  resolveServerBaseUrl,
} from '@bielu/scalar-core'
import type {
  SignalRDirection,
  SignalRDocumentRef,
  SignalRHubModel,
  SignalRMessageModel,
  SignalROperationModel,
} from './types'

const SIGNALR = 'signalr'

type AnyRecord = Record<string, any>

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

  const baseUrl = resolveServerBaseUrl(firstServerHost(doc, SIGNALR))

  // Extract security schemes from the document so the console can read them at connect time
  // without re-fetching the document. Only a minimal subset is needed for auth resolution.
  const securitySchemes = extractSecuritySchemes(doc)
  const docSecuritySchemes = Object.keys(securitySchemes).length > 0 ? securitySchemes : undefined

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
      securitySchemes: docSecuritySchemes,
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
 * Resolve the configured documents (inline object or URL) and aggregate the SignalR hubs they
 * declare. Only documents that are actually AsyncAPI are considered, so OpenAPI sources in the
 * same Scalar configuration are ignored.
 */
export async function loadSignalRHubs(documents: SignalRDocumentRef[]): Promise<SignalRHubModel[]> {
  const loaded = await loadAsyncApiDocuments(documents)
  return loaded.flatMap(({ name, doc }) => parseSignalRHubs(name, doc))
}
