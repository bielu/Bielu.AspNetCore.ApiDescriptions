import type {
  ArazzoDocument,
  ArazzoFailureAction,
  ArazzoParameter,
  ArazzoReusableObject,
  ArazzoStep,
  ArazzoSuccessAction,
  ArazzoWorkflow,
} from '../types'
import { INPUTS_NODE_ID, OUTPUTS_NODE_ID } from './data-flow'
import { expressionsIn, inputsAnchor, parseExpression } from './expressions'
import { asSelector, schemaPropertyRows, type SchemaPropertyRow } from './format'
import { operationResponses, statusCodesIn } from './openapi-exchange'
import { deref, parseJsonPointer, resolveStepTarget, targetMessages, type StepTarget } from './resolve-target'
import { schemaNavigationId, targetNavigationId } from './target-link'
import { resolveReusables } from './reusable'

type AnyRecord = Record<string, any>

const isRecord = (value: unknown): value is AnyRecord => !!value && typeof value === 'object' && !Array.isArray(value)

/** One value carried by a Data flow arrow: where it is made, what it looks like, and where it is read. */
export type FlowValue = {
  name: string
  /** The Runtime Expression the producer sets it from (`$response.body#/id`), or how the consumer reads it (`$inputs.sku`). */
  source: string
  /** Where the value comes from, in words: "201 response body of POST /orders", "the workflow's inputs". */
  origin?: string
  /** The value's schema as one property row (type, format, bounds, nested properties), when it can be found. */
  schema?: SchemaPropertyRow
  /** Where the consumer uses it: "path parameter orderId", "request body", "output total". */
  uses: string[]
  /** Where to read the value's definition: its schema's page in Scalar, else the operation or channel, or the workflow's inputs. */
  link?: FlowLink
}

/** A link from a value to where it is defined: a Scalar sidebar entry of another document, or an anchor on this page. */
export type FlowLink = { label: string; navId?: string; anchor?: string }

/** A named schema of a document (`#/components/schemas/Order`), which Scalar lists as a model. */
const NAMED_SCHEMA = /^#\/components\/schemas\/[^/]+$/

/** The `$ref` of a schema node, when it is one to a named schema. */
const namedRef = (node: unknown): string | undefined =>
  isRecord(node) && typeof node.$ref === 'string' && NAMED_SCHEMA.test(node.$ref) ? node.$ref : undefined

/**
 * The schema at a JSON Pointer into the value a schema describes: `/lines/0/sku` walks properties and items.
 * The schema found is returned as written, so a `$ref` keeps the name it is shown by (`money`). The named
 * schemas passed through on the way, outermost first, are added to `refs`.
 */
const schemaAt = (root: AnyRecord, schema: unknown, segments: string[], refs: string[] = []): unknown => {
  let current = schema
  for (const segment of segments) {
    const passed = namedRef(current)
    if (passed) {
      refs.push(passed)
    }
    const node = deref(root, current)
    if (!isRecord(node)) {
      return undefined
    }
    const parts = [node, ...(['allOf', 'oneOf', 'anyOf'] as const).flatMap((key) => (Array.isArray(node[key]) ? node[key] : []))]
      .map((part) => deref(root, part))
      .filter(isRecord)
    const property = parts.find((part) => isRecord(part.properties) && segment in part.properties)
    if (property) {
      current = property.properties[segment]
    } else if (/^\d+$/.test(segment) && parts.some((part) => part.items !== undefined)) {
      current = parts.find((part) => part.items !== undefined)!.items
    } else {
      return undefined
    }
  }
  const last = namedRef(current)
  if (last) {
    refs.push(last)
  }
  return current
}

/** `$response.body#/id` → `{ base: '$response.body', segments: ['id'] }`. */
const splitPointer = (expression: string): { base: string; segments: string[] } => {
  const hash = expression.indexOf('#')
  return hash < 0 ? { base: expression, segments: [] } : { base: expression.slice(0, hash), segments: parseJsonPointer(expression.slice(hash + 1)) }
}

/** `$.lines[0].sku` or `$['lines'][0]` → `['lines', '0', 'sku']`; `undefined` for a JSONPath with filters, wildcards or recursion. */
const jsonPathSegments = (path: string): string[] | undefined => {
  const match = /^\$((?:\.[A-Za-z_$][\w$-]*|\[\d+\]|\['[^']*'\]|\["[^"]*"\])*)$/.exec(path.trim())
  return match ? [...match[1]!.matchAll(/\.([A-Za-z_$][\w$-]*)|\[(\d+)\]|\['([^']*)'\]|\["([^"]*)"\]/g)].map((part) => part[1] ?? part[2] ?? part[3] ?? part[4]!) : undefined
}

/**
 * A step output as where it reads from and the path into that: a Runtime Expression (`$response.body#/id`), or
 * (Arazzo 1.1) a Selector Object, `{ context: '$response.body', selector: '$.total', type: 'jsonpath' }`. A
 * selector this cannot follow (an XPath, a JSONPath with filters) still names its context.
 */
const outputOf = (
  step: ArazzoStep,
  name: string,
): { text: string; base: string; segments?: string[]; selected?: boolean } | undefined => {
  const value = step.outputs?.[name]
  if (typeof value === 'string') {
    return { text: value, ...splitPointer(value.trim()) }
  }
  const selector = asSelector(value)
  if (!selector) {
    return undefined
  }
  const type = typeof selector.type === 'string' ? selector.type : selector.type.type
  const segments = type === 'jsonpath' ? jsonPathSegments(selector.selector) : type === 'jsonpointer' ? parseJsonPointer(selector.selector) : undefined
  // `text` is for display only; `base` (the context) and `segments` (the selector) are what is followed.
  return { text: `${selector.context} · ${selector.selector}`, base: selector.context.trim(), selected: true, ...(segments ? { segments } : {}) }
}

/** A schema found for a value, with the document its `$ref`s resolve against and where it is defined. */
type Found = { schema?: unknown; root?: unknown; origin?: string; link?: FlowLink }

/** The page of the innermost named schema the value is in, else the operation or channel the step calls. */
const targetLink = (target: StepTarget | undefined, refs: string[], documents: Record<string, unknown>): FlowLink | undefined => {
  const name = target?.documentName
  for (const ref of [...refs].reverse()) {
    const navId = name ? schemaNavigationId(name, ref, documents) : undefined
    if (navId) {
      return { label: `Schema ${ref.split('/').pop()}`, navId }
    }
  }
  const navId = targetNavigationId(target, documents)
  if (!navId || !target) {
    return undefined
  }
  const label =
    target.kind === 'operation' && target.method && target.path
      ? `${target.method.toUpperCase()} ${target.path}`
      : target.kind === 'channel'
        ? `Channel ${target.channelName ?? ''}`.trim()
        : 'Its definition'
  return { label, navId }
}

/** The response a step's outputs are read from: the status its success criteria check for, else the first 2XX. */
const successResponse = (step: ArazzoStep, responses: ReturnType<typeof operationResponses>) => {
  const [expected] = statusCodesIn(step.successCriteria)
  return (
    responses.find((response) => response.status === expected) ??
    responses.find((response) => /^2/.test(response.status)) ??
    responses[0]
  )
}

/** What a step output's expression reads from the step's target: a response body or header, or a message payload. */
const stepOutputSchema = (
  document: ArazzoDocument,
  workflow: ArazzoWorkflow,
  step: ArazzoStep,
  name: string,
  documents: Record<string, unknown>,
  depth: number,
  workflowNavId?: string,
): Found => {
  const output = outputOf(step, name)
  if (!output) {
    return {}
  }
  const { base, segments } = output
  const target = resolveStepTarget(document, step, documents)
  if (base === '$response.body') {
    const response = successResponse(step, operationResponses(target, documents))
    const content = response?.contents.find((candidate) => /json/i.test(candidate.contentType)) ?? response?.contents[0]
    const operation = target?.kind === 'operation' && target.method && target.path ? ` of ${target.method.toUpperCase()} ${target.path}` : ''
    // Walked from the `$ref` the response gives, so the named schema the value is in (`Order`) can be linked, with
    // the schema it resolved to alongside: an external `$ref` cannot be followed from the reference alone.
    const refs: string[] = []
    const start = content?.schemaRef ? { $ref: content.schemaRef, ...(content.schema ? { '$ref-value': content.schema } : {}) } : content?.schema
    const schema = content && segments ? schemaAt(response.root, start, segments, refs) : undefined
    const link = targetLink(target, refs, documents)
    return response
      ? { schema, root: response.root, origin: `${response.status} response body${operation}`, ...(link ? { link } : {}) }
      : { origin: 'the response body' }
  }
  if (base.startsWith('$response.header.')) {
    const link = targetLink(target, [], documents)
    return { origin: `response header ${base.slice('$response.header.'.length)}`, ...(link ? { link } : {}) }
  }
  if (base === '$message.payload') {
    const message = targetMessages(target, documents)[0]
    const refs: string[] = []
    const schema = message && isRecord(message.payload) && segments ? schemaAt(message.root, message.payload, segments, refs) : undefined
    const link = targetLink(target, refs, documents)
    return message && isRecord(message.payload)
      ? { schema, root: message.root, origin: `payload of message ${message.title || message.name}`, ...(link ? { link } : {}) }
      : { origin: 'the message payload' }
  }
  if (base.startsWith('$message.header.')) {
    const link = targetLink(target, [], documents)
    return { origin: `message header ${base.slice('$message.header.'.length)}`, ...(link ? { link } : {}) }
  }
  // An output passed on from an input or another step's output: describe it where it came from. The expression
  // (a Selector Object's context) is followed on its own, then the pointer or selector narrows what it found.
  if (depth >= 4) {
    return {}
  }
  const forwarded = sourceSchema(document, workflow, base, documents, depth + 1, workflowNavId)
  if (!segments) {
    // A selector this cannot follow selects some part of the context, whose schema is not known.
    return output.selected ? { ...forwarded, schema: undefined } : forwarded
  }
  return { ...forwarded, schema: isRecord(forwarded.root) ? schemaAt(forwarded.root, forwarded.schema, segments) : undefined }
}

/**
 * The schema of an input of the workflow, with the Arazzo document (`#/components/inputs/…`) and the schema
 * (`#/$defs/…`) as root, linked to the input's row on this page when the workflow's section id is known.
 */
const inputSchema = (document: ArazzoDocument, workflow: ArazzoWorkflow, segments: string[], workflowNavId: string | undefined): Found => {
  const arazzo = document as unknown as AnyRecord
  const inputs = deref(arazzo, workflow.inputs)
  const link = workflowNavId ? { link: { label: "The workflow's inputs", anchor: inputsAnchor(workflowNavId, segments[0]) } } : {}
  if (!isRecord(inputs)) {
    return { origin: "the workflow's inputs", ...link }
  }
  // Walked against both, so the inputs schema's own `#/$defs/…` resolve as well as the document's components.
  const root = { ...arazzo, ...inputs }
  return { schema: schemaAt(root, inputs, segments), root, origin: "the workflow's inputs", ...link }
}

/** Where an expression's value is made, followed back to a schema when it can be. */
const sourceSchema = (
  document: ArazzoDocument,
  workflow: ArazzoWorkflow,
  expression: string,
  documents: Record<string, unknown>,
  depth: number,
  workflowNavId?: string,
): Found => {
  const parsed = parseExpression(expression)
  if (parsed.source === 'inputs') {
    return inputSchema(document, workflow, [parsed.name, ...splitPointer(expression).segments], workflowNavId)
  }
  if (parsed.source === 'steps' && parsed.field === 'outputs' && parsed.name) {
    const step = workflow.steps?.find((candidate) => candidate.stepId === parsed.stepId)
    const found = step ? stepOutputSchema(document, workflow, step, parsed.name, documents, depth, workflowNavId) : {}
    const { segments } = splitPointer(expression)
    return segments.length && isRecord(found.root) ? { ...found, schema: schemaAt(found.root, found.schema, segments) } : found
  }
  if (parsed.source === 'workflows' && parsed.field === 'outputs') {
    return { origin: `outputs of workflow ${parsed.workflowId}` }
  }
  return {}
}

/** Whether an expression reads `name` from the producer node. */
const readsFrom = (expression: string, producer: string, name: string): boolean => {
  const parsed = parseExpression(expression)
  if (producer === INPUTS_NODE_ID) {
    return parsed.source === 'inputs' && parsed.name === name
  }
  if (producer.startsWith('::workflow:')) {
    return parsed.source === 'workflows' && parsed.workflowId === producer.slice('::workflow:'.length) && parsed.name === name
  }
  return parsed.source === 'steps' && parsed.stepId === producer && parsed.field === 'outputs' && parsed.name === name
}

/** The expressions in any value: a string, or the strings nested in an object or array (a request body payload). */
const expressionsOf = (value: unknown): string[] =>
  typeof value === 'string'
    ? expressionsIn(value)
    : Array.isArray(value)
      ? value.flatMap(expressionsOf)
      : isRecord(value)
        ? Object.values(value).flatMap(expressionsOf)
        : []

/**
 * The places a step's `onSuccess` or `onFailure` actions read a value: each action's criteria and the parameters
 * it passes to the workflow it goes to, components followed, as the Data flow graph counts them.
 */
const actionPlaces = (
  document: ArazzoDocument,
  kind: 'success' | 'failure',
  actions: (ArazzoSuccessAction | ArazzoFailureAction | ArazzoReusableObject)[] | undefined,
): { place: string; value: unknown }[] =>
  resolveReusables<ArazzoSuccessAction | ArazzoFailureAction>(document, actions).flatMap((action) => {
    const label = action.name ? `${kind} action ${action.name}` : `${kind} action`
    return [
      ...(action.criteria ?? []).map((criterion) => ({ place: `${label} criteria`, value: criterion.condition })),
      ...resolveReusables<ArazzoParameter>(document, action.parameters).map((parameter) => ({ place: `${label} parameter ${parameter.name}`, value: parameter.value })),
    ]
  })

/** The places of a consumer that read a value, with the first expression that does. */
const placesIn = (
  document: ArazzoDocument,
  workflow: ArazzoWorkflow,
  consumer: string,
): { place: string; value: unknown }[] => {
  if (consumer === OUTPUTS_NODE_ID) {
    return Object.entries(workflow.outputs ?? {}).map(([name, value]) => ({ place: `workflow output ${name}`, value }))
  }
  const step = workflow.steps?.find((candidate) => candidate.stepId === consumer)
  if (!step) {
    return []
  }
  const parameters = resolveReusables<ArazzoParameter>(document, step.parameters)
  return [
    ...parameters.map((parameter) => ({ place: `${parameter.in ? `${parameter.in} ` : ''}parameter ${parameter.name}`, value: parameter.value })),
    { place: 'request body', value: step.requestBody?.payload },
    ...(step.requestBody?.replacements ?? []).map((replacement) => ({ place: `request body ${replacement.target}`, value: replacement.value })),
    { place: 'correlation', value: step.correlationId },
    ...(step.successCriteria ?? []).map((criterion) => ({ place: 'success criteria', value: criterion.condition })),
    ...actionPlaces(document, 'success', step.onSuccess),
    ...actionPlaces(document, 'failure', step.onFailure),
    ...Object.entries(step.outputs ?? {}).map(([name, value]) => ({ place: `output ${name}`, value })),
  ]
}

/**
 * The values a Data flow arrow carries from `producer` to `consumer`, each with where it is made (the
 * producer's output expression and the response or message it reads, or the workflow's inputs), its schema
 * as far as the loaded documents describe it, and where the consumer uses it. A value whose schema cannot be
 * found (its document isn't loaded, a header, another workflow's output) still says where it comes from.
 * Each value links to where it is defined: its named schema's page in Scalar, else the operation or channel
 * that makes it, or (with `workflowNavId`, the workflow's section id) its row in the workflow's inputs.
 */
export const describeFlow = (
  document: ArazzoDocument,
  workflow: ArazzoWorkflow,
  producer: string,
  consumer: string,
  names: string[],
  documents: Record<string, unknown>,
  workflowNavId?: string,
): FlowValue[] =>
  names.map((name) => {
    const places = placesIn(document, workflow, consumer).flatMap(({ place, value }) => {
      const reading = expressionsOf(value).find((expression) => readsFrom(expression, producer, name))
      return reading ? [{ place, reading }] : []
    })
    const step = workflow.steps?.find((candidate) => candidate.stepId === producer)
    const produced = step ? outputOf(step, name)?.text : undefined
    const reading = places[0]?.reading
    // Described where it is made; a reading with a pointer into it (`$steps.a.outputs.order#/id`) narrows it.
    const own = producer === INPUTS_NODE_ID
      ? `$inputs.${name}`
      : producer.startsWith('::workflow:')
        ? `$workflows.${producer.slice('::workflow:'.length)}.outputs.${name}`
        : `$steps.${producer}.outputs.${name}`
    const found = sourceSchema(document, workflow, reading?.includes('#') ? reading : own, documents, 0, workflowNavId)
    const rows = found.schema !== undefined ? schemaPropertyRows({ type: 'object', properties: { [name]: found.schema } }, found.root) : undefined
    return {
      name,
      source: produced ?? reading ?? name,
      ...(found.origin ? { origin: found.origin } : {}),
      ...(rows?.[0] ? { schema: rows[0] } : {}),
      uses: [...new Set(places.map(({ place }) => place))],
      ...(found.link ? { link: found.link } : {}),
    }
  })
