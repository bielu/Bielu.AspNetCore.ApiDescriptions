import type { ArazzoDocument, ArazzoSourceDescription, ArazzoStep, ArazzoWorkflow } from '../types'

type AnyRecord = Record<string, any>

/** A step that invokes another workflow of the same Arazzo document. */
export type WorkflowTarget = {
  kind: 'workflow'
  identifier: string
  workflow: ArazzoWorkflow | undefined
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
const deref = (root: AnyRecord, node: unknown, depth = 0): unknown => {
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
  const wanted = absoluteUrl(source.url, absoluteUrl(arazzo['x-scalar-original-source-url'], page) ?? page)
  if (!wanted) {
    return undefined
  }
  for (const [name, document] of Object.entries(documents)) {
    if (isRecord(document) && absoluteUrl(document['x-scalar-original-source-url'], page) === wanted) {
      return { name, document, sourceName: source.name }
    }
  }
  return undefined
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
      return asyncApiOperationTarget(loaded, operationId, operations[id])
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
    return asyncApiOperationTarget(loaded, value, node)
  }
  return undefined
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
    return {
      kind: 'workflow',
      identifier: step.workflowId,
      workflow: arazzo.workflows?.find((workflow) => workflow.workflowId === step.workflowId),
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
    const pathItem = target.path ? walk(root, ['paths', target.path]) : undefined
    const declared = [
      ...(isRecord(pathItem) && Array.isArray(pathItem.parameters) ? pathItem.parameters : []),
      ...(Array.isArray(target.operation?.parameters) ? target.operation.parameters : []),
    ]
    const byKey = new Map<string, TargetParameter>()
    for (const entry of declared) {
      const parameter = deref(root, entry)
      if (!isRecord(parameter) || typeof parameter.name !== 'string') {
        continue
      }
      const location = typeof parameter.in === 'string' ? parameter.in : 'query'
      byKey.set(`${location}:${parameter.name}`, {
        name: parameter.name,
        location,
        required: parameter.required === true || location === 'path',
        type: parameterType(deref(root, parameter.schema)),
        description: typeof parameter.description === 'string' ? parameter.description : '',
      })
    }
    return [...byKey.values()]
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
