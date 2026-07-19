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
  GrpcDocumentRef,
  GrpcMessageModel,
  GrpcMethodModel,
  GrpcMethodTypeName,
  GrpcServiceModel,
} from './types'

const GRPC = 'grpc'

const METHOD_TYPES: ReadonlySet<string> = new Set([
  'unary',
  'serverStreaming',
  'clientStreaming',
  'bidirectionalStreaming',
])

type AnyRecord = Record<string, any>

/** Resolve the message(s) an operation carries and generate an example payload for each. */
function buildMessages(doc: AnyRecord, op: AnyRecord): GrpcMessageModel[] {
  const refs = (Array.isArray(op.messages) ? op.messages : [])
    .map((m: AnyRecord) => m?.$ref)
    .filter((ref: unknown): ref is string => typeof ref === 'string')

  const messages: GrpcMessageModel[] = []
  for (const ref of refs) {
    const message = deref(doc, { $ref: ref })
    const name = message?.name ?? refName(ref) ?? 'message'
    // A gRPC request is a single protobuf message; the example is one JSON object (proto3 JSON).
    const payloadExample = exampleFromSchema(doc, message?.payload) ?? {}
    messages.push({ name, title: message?.title, example: JSON.stringify(payloadExample, null, 2) })
  }
  return messages
}

function methodTypeFor(binding: AnyRecord | undefined): GrpcMethodTypeName | undefined {
  const value = binding?.methodType
  return typeof value === 'string' && METHOD_TYPES.has(value) ? (value as GrpcMethodTypeName) : undefined
}

/** Extract every gRPC service (channel + its operations) from a parsed AsyncAPI document. */
export function parseGrpcServices(documentName: string, doc: AnyRecord): GrpcServiceModel[] {
  if (!doc || typeof doc !== 'object') {
    return []
  }

  const baseUrl = resolveServerBaseUrl(firstServerHost(doc, GRPC))

  // Extract security schemes from the document so the console can read them at invoke time
  // without re-fetching the document. Only a minimal subset is needed for auth resolution.
  const securitySchemes = extractSecuritySchemes(doc)
  const docSecuritySchemes = Object.keys(securitySchemes).length > 0 ? securitySchemes : undefined

  const channels: AnyRecord = doc.channels ?? {}
  const services = new Map<string, GrpcServiceModel>()

  for (const channelName of Object.keys(channels)) {
    const channel: AnyRecord = channels[channelName] ?? {}
    const binding: AnyRecord | undefined = channel.bindings?.[GRPC]
    if (!binding) {
      continue
    }
    services.set(channelName, {
      documentName,
      channelName,
      service: binding.service ?? channelName,
      package: binding.package,
      protoFile: binding.protoFile,
      baseUrl,
      methods: [],
      securitySchemes: docSecuritySchemes,
    })
  }

  const operations: AnyRecord = doc.operations ?? {}
  for (const opName of Object.keys(operations)) {
    const op: AnyRecord = operations[opName] ?? {}
    const channelName = refName(op.channel?.$ref)
    const service = channelName ? services.get(channelName) : undefined
    if (!service) {
      continue
    }
    const binding: AnyRecord | undefined = op.bindings?.[GRPC]
    const model: GrpcMethodModel = {
      id: opName,
      method: binding?.method ?? opName,
      methodType: methodTypeFor(binding),
      requestType: binding?.requestType,
      responseType: binding?.responseType,
      idempotencyLevel: binding?.idempotencyLevel,
      deadlineSeconds: typeof binding?.deadlineSeconds === 'number' ? binding.deadlineSeconds : undefined,
      summary: op.summary ?? op.description,
      messages: buildMessages(doc, op),
    }
    service.methods.push(model)
  }

  return [...services.values()]
}

/**
 * Resolve the configured documents (inline object or URL) and aggregate the gRPC services they
 * declare. Only documents that are actually AsyncAPI are considered, so OpenAPI sources in the
 * same Scalar configuration are ignored.
 */
export async function loadGrpcServices(documents: GrpcDocumentRef[]): Promise<GrpcServiceModel[]> {
  const loaded = await loadAsyncApiDocuments(documents)
  return loaded.flatMap(({ name, doc }) => parseGrpcServices(name, doc))
}
