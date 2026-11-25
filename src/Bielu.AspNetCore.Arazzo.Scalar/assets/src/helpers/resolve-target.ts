import type { ArazzoDocument, ArazzoSourceDescription, ArazzoStep, ArazzoWorkflow } from '../types'
import { normalizeArazzoDocument } from './normalize'

type AnyRecord = Record<string, any>

/** A step that invokes a workflow: one of this Arazzo document, or (`$sourceDescriptions.<name>.<id>`) of another loaded one. */
export type WorkflowTarget = {
  kind: 'workflow'
  identifier: string
  workflow: ArazzoWorkflow | undefined
  /** The workspace name of the Arazzo document the workflow is in, when it is another loaded document. */
  documentName?: string
  /** For `$sourceDescriptions.<name>.<id>`: the source description it names. */
  sourceName?: string
  /** The `arazzo` version of the document the workflow was found in. */
  version?: string
}

/** A step that calls an OpenAPI operation of a loaded sibling document. */
export type OperationTarget = {
  kind: 'operation'
  identifier: string
  /** The `sourceDescriptions[].name` the target belongs to, when the step or the resolved document says. */
  sourceName: string | undefined
  documentName: string | undefined
  method: string | undefined
  path: string | undefined
  operation: AnyRecord | undefined
}

/** A step that targets an AsyncAPI operation or channel of a loaded sibling document. */
export type ChannelTarget = {
  kind: 'channel'
  identifier: string
  /** The `sourceDescriptions[].name` the target belongs to, when the step or the resolved document says. */
  sourceName: string | undefined
  documentName: string | undefined
  channelName: string | undefined
  channel: AnyRecord | undefined
  /** The key of the AsyncAPI operation on `operations`, when the step targets an operation rather than a channel. */
  operationName?: string
  operation: AnyRecord | undefined
}

/**
 * What a step targets. `identifier` is always the raw `operationId`/`operationPath`/`channelPath`/
 * `workflowId`, so the UI can show what the step points at even when the target is not loaded.
 */
export type StepTarget = WorkflowTarget | OperationTarget | ChannelTarget

/** The only Runtime Expression form `operationPath`/`channelPath` use: `{$sourceDescriptions.NAME.url}#/POINTER`. */
export const SOURCE_DESCRIPTION_POINTER = /^\{\$sourceDescriptions\.([^.}]+)\.url\}#(\/.*)?$/

const HTTP_METHODS = new Set(['get', 'put', 'post', 'delete', 'options', 'head', 'patch', 'trace'])

const isRecord = (value: unknown): value is AnyRecord => !!value && typeof value === 'object'

/** Splits a JSON Pointer (RFC 6901, possibly percent-encoded as a URI fragment) into its segments. */
export const parseJsonPointer = (pointer: string): string[] =>
  pointer
    .split('/')
    .slice(1)
    .map((segment) => {
      let decoded = segment
      try {
        decoded = decodeURIComponent(segment)
      } catch {
        // Not percent-encoded after all: use the segment as written.
      }
      return decoded.replace(/~1/g, '/').replace(/~0/g, '~')
    })

/**
 * Follows a JSON Pointer through `root`, dereferencing each node on the way, so a pointer can pass
 * through a referenced Path Item (`/paths/~1pets/get` where `/paths/~1pets` is a `$ref`).
 */
const walk = (root: AnyRecord, segments: string[], depth = 0): unknown =>
  segments.reduce<unknown>((node, segment) => deref(root, isRecord(node) ? node[segment] : undefined, depth), root)

/**
 * Follows a `$ref`. Scalar's workspace store exposes the resolved target as `$ref-value`; a plain
 * local `#/…` reference is followed against the document itself as a fallback.
 */
export const deref = (root: AnyRecord, node: unknown, depth = 0): unknown => {
  if (!isRecord(node) || typeof node.$ref !== 'string' || depth > 16) {
    return node
  }
  if (isRecord(node['$ref-value'])) {
    return deref(root, node['$ref-value'], depth + 1)
  }
  return node.$ref.startsWith('#') ? walk(root, parseJsonPointer(node.$ref.slice(1)), depth + 1) : undefined
}

/** Resolves a possibly relative URL to an absolute one, or `undefined` when it cannot be parsed. */
const absoluteUrl = (url: string | undefined, base: string | undefined): string | undefined => {
  if (!url) {
    return undefined
  }
  try {
    return new URL(url, base).href
  } catch {
    return undefined
  }
}

/** The page URL, used as the base for relative document URLs. */
const pageUrl = (): string | undefined => (typeof location === 'undefined' ? undefined : location.href)

type LoadedDocument = { name: string; document: AnyRecord; sourceName: string }

/**
 * The absolute URL a source description of `arazzo` points at, resolved against Arazzo's base URI: the document's
 * `$self` when it has one (itself resolved against where the document was retrieved from), else the retrieval URL.
 * Two documents' sources point at the same document when their URLs are equal, whatever the sources are called.
 */
export const sourceUrl = (arazzo: ArazzoDocument, source: ArazzoSourceDescription): string | undefined => {
  const page = pageUrl()
  const retrieved = absoluteUrl(arazzo['x-scalar-original-source-url'], page) ?? page
  return absoluteUrl(source.url, absoluteUrl(arazzo.$self, retrieved) ?? retrieved)
}

/**
 * Finds the loaded workspace document a `sourceDescriptions[].url` refers to. Both sides are
 * resolved to absolute URLs first: our generator writes app-relative URLs (`/asyncapi/v1.json`),
 * while Scalar records each source the way it was configured.
 */
export const findDocumentByUrl = (
  arazzo: ArazzoDocument,
  documents: Record<string, unknown>,
  source: ArazzoSourceDescription,
): LoadedDocument | undefined => {
  const page = pageUrl()
  const wanted = sourceUrl(arazzo, source)
  if (!wanted) {
    return undefined
  }
  for (const [name, document] of Object.entries(documents)) {
    if (isRecord(document) && documentUrls(document, page).includes(wanted)) {
      return { name, document, sourceName: source.name }
    }
  }
  return undefined
}

/**
 * The absolute URLs a loaded document answers to: where Scalar loaded it from, and its own `$self` (OpenAPI 3.2's
 * canonical URI, resolved against the former). A document Scalar fetched through a proxy or service discovery
 * still matches a source that names its public URL, when the document says that URL is its `$self`.
 */
const documentUrls = (document: AnyRecord, page: string | undefined): (string | undefined)[] => {
  const retrieved = absoluteUrl(document['x-scalar-original-source-url'], page)
  return [retrieved, absoluteUrl(document.$self, retrieved ?? page)]
}

export const isOpenApi = (document: AnyRecord): boolean => typeof document.openapi === 'string' || typeof document.swagger === 'string'

export const isAsyncApi = (document: AnyRecord): boolean => typeof document.asyncapi === 'string'

/** The channel name an AsyncAPI 3 operation's `channel: { $ref: '#/channels/NAME' }` points at. */
const channelNameOf = (operation: AnyRecord | undefined): string | undefined => {
  const ref = isRecord(operation?.channel) ? operation.channel.$ref : undefined
  const segments = typeof ref === 'string' && ref.startsWith('#') ? parseJsonPointer(ref.slice(1)) : []
  return segments[0] === 'channels' ? segments[1] : undefined
}

const asyncApiOperationTarget = (
  loaded: LoadedDocument,
  identifier: string,
  operationName: string,
  operationNode: unknown,
): ChannelTarget => {
  const operation = deref(loaded.document, operationNode) as AnyRecord | undefined
  const channelName = channelNameOf(operation)
  return {
    kind: 'channel',
    identifier,
    sourceName: loaded.sourceName,
    documentName: loaded.name,
    channelName,
    channel: channelName ? (deref(loaded.document, loaded.document.channels?.[channelName]) as AnyRecord) : undefined,
    operationName,
    operation,
  }
}

/**
 * Resolves `operationId`. The Arazzo spec requires it to be unique across all source descriptions,
 * so every source is searched in declaration order. It may also be qualified as
 * `$sourceDescriptions.NAME.operationId` when ids collide, which narrows the search to that source.
 */
const findByOperationId = (
  arazzo: ArazzoDocument,
  documents: Record<string, unknown>,
  operationId: string,
): OperationTarget | ChannelTarget | undefined => {
  const qualified = /^\$sourceDescriptions\.([^.]+)\.(.+)$/.exec(operationId)
  const id = qualified ? qualified[2]! : operationId
  const sources = (arazzo.sourceDescriptions ?? []).filter((source) => !qualified || source.name === qualified[1])

  for (const source of sources) {
    const loaded = findDocumentByUrl(arazzo, documents, source)
    if (!loaded) {
      continue
    }

    if (isOpenApi(loaded.document)) {
      for (const [path, pathItemNode] of Object.entries<unknown>(loaded.document.paths ?? {})) {
        const pathItem = deref(loaded.document, pathItemNode)
        if (!isRecord(pathItem)) {
          continue
        }
        for (const [method, operationNode] of Object.entries<unknown>(pathItem)) {
          const operation = HTTP_METHODS.has(method) ? deref(loaded.document, operationNode) : undefined
          if (isRecord(operation) && operation.operationId === id) {
            return {
              kind: 'operation',
              identifier: operationId,
              sourceName: loaded.sourceName,
              documentName: loaded.name,
              method,
              path,
              operation,
            }
          }
        }
      }
    }

    // AsyncAPI 3 operations have no `operationId` field: the key on `operations` is the id.
    // Own keys only: an inherited name such as `constructor` is not a declared operation.
    const operations = isAsyncApi(loaded.document) ? loaded.document.operations : undefined
    if (operations && Object.prototype.hasOwnProperty.call(operations, id)) {
      return asyncApiOperationTarget(loaded, operationId, id, operations[id])
    }
  }

  return undefined
}

/**
 * The source description a raw step reference names: `{$sourceDescriptions.NAME.url}#/…` for
 * `operationPath`/`channelPath`, or a qualified `$sourceDescriptions.NAME.operationId`. A bare
 * `operationId` names none.
 */
export const sourceNameOf = (reference: string): string | undefined =>
  SOURCE_DESCRIPTION_POINTER.exec(reference)?.[1] ?? /^\$sourceDescriptions\.([^.]+)\./.exec(reference)?.[1]

/** Resolves `operationPath`/`channelPath`, which name their source explicitly. */
const findByPointer = (
  arazzo: ArazzoDocument,
  documents: Record<string, unknown>,
  value: string,
): OperationTarget | ChannelTarget | undefined => {
  const match = SOURCE_DESCRIPTION_POINTER.exec(value)
  const source = match ? arazzo.sourceDescriptions?.find((candidate) => candidate.name === match[1]) : undefined
  const loaded = source ? findDocumentByUrl(arazzo, documents, source) : undefined
  if (!match || !loaded) {
    return undefined
  }

  const segments = parseJsonPointer(match[2] ?? '')
  const node = walk(loaded.document, segments)
  if (!isRecord(node)) {
    return undefined
  }

  // /paths/<path>/<method>
  if (segments[0] === 'paths' && segments.length === 3) {
    return {
      kind: 'operation',
      identifier: value,
      sourceName: loaded.sourceName,
      documentName: loaded.name,
      path: segments[1],
      method: segments[2],
      operation: node,
    }
  }
  // /channels/<name>
  if (segments[0] === 'channels' && segments.length === 2) {
    return {
      kind: 'channel',
      identifier: value,
      sourceName: loaded.sourceName,
      documentName: loaded.name,
      channelName: segments[1],
      channel: node,
      operation: undefined,
    }
  }
  // /operations/<id>
  if (segments[0] === 'operations' && segments.length === 2) {
    return asyncApiOperationTarget(loaded, value, segments[1]!, node)
  }
  return undefined
}

/**
 * Whether every document the Arazzo document can resolve into is loaded: each of its source descriptions, and the
 * Arazzo sources of every Arazzo document reached through them, so a workflow called across documents can be drawn
 * all the way down. Each document is visited once, so sources that call back into each other end.
 */
export const sourcesLoaded = (arazzo: ArazzoDocument, documents: Record<string, unknown>): boolean => {
  const visited = new Set<ArazzoDocument>()
  const queue: { document: ArazzoDocument; root: boolean }[] = [{ document: arazzo, root: true }]
  for (let next = queue.shift(); next; next = queue.shift()) {
    if (visited.has(next.document)) {
      continue
    }
    visited.add(next.document)
    for (const source of next.document.sourceDescriptions ?? []) {
      // Beyond the document itself only Arazzo sources matter: they hold the workflows drawn inside it.
      if (!next.root && source.type !== 'arazzo') {
        continue
      }
      const loaded = findDocumentByUrl(next.document, documents, source)
      if (!loaded) {
        return false
      }
      const other = typeof loaded.document.arazzo === 'string' ? normalizeArazzoDocument(loaded.document) : undefined
      if (other) {
        queue.push({ document: other, root: false })
      }
    }
  }
  return true
}

/** `$sourceDescriptions.<name>.<workflowId>`: a workflow of the Arazzo document a source description names. */
export const QUALIFIED_WORKFLOW = /^\$sourceDescriptions\.([^.]+)\.(.+)$/

/** A workflow found by its `workflowId`, with the Arazzo document it belongs to. */
export type ResolvedWorkflow = {
  /** The document the workflow is in: its components, page registry and step targets resolve there. */
  document: ArazzoDocument
  workflow: ArazzoWorkflow
  /** The workspace name of that document, when it is not the document the lookup started from. */
  documentName?: string
}

/**
 * Finds the workflow a `workflowId` names: a plain id in `arazzo` itself, or a qualified
 * `$sourceDescriptions.<name>.<id>` in the Arazzo document that source description points at, when Scalar
 * has loaded it. A qualified id always means the other document, even when `arazzo` has a workflow of
 * the same id. `undefined` when the workflow is not found or its document is not loaded.
 */
export const resolveWorkflow = (
  arazzo: ArazzoDocument,
  workflowId: string | undefined,
  documents: Record<string, unknown> = {},
): ResolvedWorkflow | undefined => {
  if (!workflowId) {
    return undefined
  }
  const qualified = QUALIFIED_WORKFLOW.exec(workflowId)
  if (!qualified) {
    const workflow = workflowId.startsWith('$') ? undefined : arazzo.workflows?.find((candidate) => candidate.workflowId === workflowId)
    return workflow ? { document: arazzo, workflow } : undefined
  }
  const source = arazzo.sourceDescriptions?.find((candidate) => candidate.name === qualified[1])
  if (!source || (source.type && source.type !== 'arazzo')) {
    return undefined
  }
  const loaded = findDocumentByUrl(arazzo, documents, source)
  const other = loaded ? normalizeArazzoDocument(loaded.document) : undefined
  const workflow = other?.workflows?.find((candidate) => candidate.workflowId === qualified[2])
  return other && workflow ? { document: other, workflow, documentName: loaded!.name } : undefined
}

/**
 * Resolves whichever of `operationId`/`operationPath`/`channelPath`/`workflowId` a step declares.
 * Never throws: when the target's document is not loaded (or the id was renamed) the result still
 * carries the raw `identifier`, with the resolved fields left `undefined`.
 */
export const resolveStepTarget = (
  arazzo: ArazzoDocument,
  step: ArazzoStep,
  documents: Record<string, unknown>,
): StepTarget | undefined => {
  if (step.workflowId) {
    const resolved = resolveWorkflow(arazzo, step.workflowId, documents)
    const sourceName = QUALIFIED_WORKFLOW.exec(step.workflowId)?.[1]
    return {
      kind: 'workflow',
      identifier: step.workflowId,
      workflow: resolved?.workflow,
      ...(resolved?.documentName ? { documentName: resolved.documentName } : {}),
      ...(sourceName ? { sourceName } : {}),
      ...(resolved ? { version: resolved.document.arazzo } : {}),
    }
  }

  const unresolvedOperation = (identifier: string): OperationTarget => ({
    kind: 'operation',
    identifier,
    sourceName: sourceNameOf(identifier),
    documentName: undefined,
    method: undefined,
    path: undefined,
    operation: undefined,
  })

  if (step.operationId) {
    return findByOperationId(arazzo, documents, step.operationId) ?? unresolvedOperation(step.operationId)
  }
  if (step.operationPath) {
    return findByPointer(arazzo, documents, step.operationPath) ?? unresolvedOperation(step.operationPath)
  }
  if (step.channelPath) {
    return (
      findByPointer(arazzo, documents, step.channelPath) ?? {
        kind: 'channel',
        identifier: step.channelPath,
        sourceName: sourceNameOf(step.channelPath),
        documentName: undefined,
        channelName: undefined,
        channel: undefined,
        operation: undefined,
      }
    )
  }
  return undefined
}

/** A parameter of the operation or channel a step targets, for display next to the step. */
export type TargetParameter = {
  name: string
  /** `path`, `query`, `header`, `cookie` (OpenAPI) or `channel` (AsyncAPI). */
  location: string
  required: boolean
  type: string
  description: string
}

const parameterType = (schema: unknown): string => {
  if (!isRecord(schema)) {
    return 'string'
  }
  const type = Array.isArray(schema.type) ? schema.type.join(' | ') : schema.type
  const format = typeof schema.format === 'string' ? ` (${schema.format})` : ''
  return typeof type === 'string' ? `${type}${format}` : 'string'
}

/**
 * The Parameter Objects an OpenAPI operation target accepts, references followed in `root`: the path
 * item's, overridden by the operation's own (matched on name and location, as OpenAPI and Swagger 2.0
 * specify), in declaration order.
 */
export const operationParameters = (root: AnyRecord, target: OperationTarget): AnyRecord[] => {
  const pathItem = target.path ? walk(root, ['paths', target.path]) : undefined
  const declared = [
    ...(isRecord(pathItem) && Array.isArray(pathItem.parameters) ? pathItem.parameters : []),
    ...(Array.isArray(target.operation?.parameters) ? target.operation.parameters : []),
  ]
  const byKey = new Map<string, AnyRecord>()
  for (const entry of declared) {
    const parameter = deref(root, entry)
    if (isRecord(parameter) && typeof parameter.name === 'string') {
      byKey.set(`${typeof parameter.in === 'string' ? parameter.in : 'query'}:${parameter.name}`, parameter)
    }
  }
  return [...byKey.values()]
}

/**
 * The parameters of a resolved target, so a step shows what the operation or channel it calls
 * accepts. OpenAPI: the path item's parameters, overridden by the operation's own (matched on name
 * and location, as OpenAPI specifies). AsyncAPI: the channel's address parameters, always required.
 * References are followed within the target's document. Empty for an unresolved target.
 */
export const targetParameters = (target: StepTarget | undefined, documents: Record<string, unknown>): TargetParameter[] => {
  if (!target || target.kind === 'workflow' || !target.documentName) {
    return []
  }
  const root = documents[target.documentName]
  if (!isRecord(root)) {
    return []
  }

  if (target.kind === 'operation') {
    return operationParameters(root, target).map((parameter) => {
      const location = typeof parameter.in === 'string' ? parameter.in : 'query'
      return {
        name: parameter.name,
        location,
        required: parameter.required === true || location === 'path',
        type: parameterType(deref(root, parameter.schema)),
        description: typeof parameter.description === 'string' ? parameter.description : '',
      }
    })
  }

  const parameters = isRecord(target.channel?.parameters) ? target.channel.parameters : {}
  return Object.entries(parameters).map(([name, entry]) => {
    const parameter = deref(root, entry)
    const description = isRecord(parameter) && typeof parameter.description === 'string' ? parameter.description : ''
    return { name, location: 'channel', required: true, type: 'string', description }
  })
}

/** The description of a resolved target, falling back to its summary (or a channel's title). */
export const targetDescription = (target: StepTarget | undefined): string => {
  if (!target) {
    return ''
  }
  const candidates =
    target.kind === 'workflow'
      ? [target.workflow?.description, target.workflow?.summary]
      : target.kind === 'operation'
        ? [target.operation?.description, target.operation?.summary]
        : [target.operation?.description, target.channel?.description, target.operation?.summary, target.channel?.title]
  return candidates.find((value): value is string => typeof value === 'string' && value.trim() !== '') ?? ''
}

/** A message an AsyncAPI step sends or receives, with its payload schema, for display next to the step. */
export type TargetMessage = {
  /** The key the message has on its channel (or in `components.messages`). */
  name: string
  title: string
  summary: string
  description: string
  contentType: string | undefined
  /**
   * The payload schema, unwrapped from a Multi Format Schema Object when it is one. A schema in a non-JSON
   * format may be text, such as the `.proto` source of a Protobuf payload, and is kept as that string.
   */
  payload: AnyRecord | string | undefined
  /** `schemaFormat` of a payload that is not plain JSON Schema (Avro, Protobuf, …); its schema is shown as written. */
  schemaFormat: string | undefined
  /** The first example payload, when the message declares one. */
  example: unknown
  /** The AsyncAPI document the message lives in, which the payload's `$ref`s resolve against. */
  root: AnyRecord
}

const JSON_SCHEMA_FORMAT = /^application\/(schema\+json|vnd\.aai\.asyncapi)/

/** The key a `#/…/messages/NAME` reference ends with. */
const lastSegment = (ref: unknown): string | undefined =>
  typeof ref === 'string' ? parseJsonPointer(ref.replace(/^#/, '')).pop() : undefined

/**
 * The messages of a resolved AsyncAPI target: those the AsyncAPI 3 operation lists when the step
 * targets an operation, else every message of the channel. References are followed within the
 * target's document, and a payload given as a Multi Format Schema Object (`{ schemaFormat, schema }`)
 * is unwrapped; its schema is kept as text when the format writes it so (Protobuf). Empty for an OpenAPI, workflow or unresolved target.
 */
export const targetMessages = (target: StepTarget | undefined, documents: Record<string, unknown>): TargetMessage[] => {
  if (target?.kind !== 'channel' || !target.documentName) {
    return []
  }
  const root = documents[target.documentName]
  if (!isRecord(root)) {
    return []
  }

  const listed = Array.isArray(target.operation?.messages) ? (target.operation.messages as unknown[]) : undefined
  const entries: [string | undefined, unknown][] = listed
    ? listed.map((entry) => [lastSegment(isRecord(entry) ? entry.$ref : undefined), entry])
    : Object.entries(isRecord(target.channel?.messages) ? target.channel.messages : {})

  const defaultContentType = typeof root.defaultContentType === 'string' ? root.defaultContentType : undefined
  return entries.flatMap(([key, entry]) => {
    const message = deref(root, entry)
    if (!isRecord(message)) {
      return []
    }
    const text = (value: unknown): string => (typeof value === 'string' ? value : '')
    let payload = deref(root, message.payload)
    let schemaFormat: string | undefined
    if (isRecord(payload) && typeof payload.schemaFormat === 'string' && 'schema' in payload) {
      schemaFormat = JSON_SCHEMA_FORMAT.test(payload.schemaFormat) ? undefined : payload.schemaFormat
      payload = deref(root, payload.schema)
    }
    const examples = Array.isArray(message.examples) ? message.examples : []
    const example = examples.map((candidate) => deref(root, candidate)).find((candidate) => isRecord(candidate) && 'payload' in candidate)
    return [
      {
        name: key ?? (text(message.name) || 'message'),
        title: text(message.title),
        summary: text(message.summary),
        description: text(message.description),
        contentType: typeof message.contentType === 'string' ? message.contentType : defaultContentType,
        payload: isRecord(payload) || (schemaFormat && typeof payload === 'string' && payload.trim()) ? payload : undefined,
        schemaFormat,
        example: isRecord(example) ? example.payload : undefined,
        root,
      },
    ]
  })
}
