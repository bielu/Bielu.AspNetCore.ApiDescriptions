import type { ArazzoDocument, ArazzoParameter, ArazzoStep, ArazzoWorkflow } from '../types'
import { INPUTS_NODE_ID, OUTPUTS_NODE_ID } from './data-flow'
import { expressionsIn, parseExpression } from './expressions'
import { schemaPropertyRows, type SchemaPropertyRow } from './format'
import { operationResponses, statusCodesIn } from './openapi-exchange'
import { deref, parseJsonPointer, resolveStepTarget, targetMessages } from './resolve-target'
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
}

/**
 * The schema at a JSON Pointer into the value a schema describes: `/lines/0/sku` walks properties and items.
 * The schema found is returned as written, so a `$ref` keeps the name it is shown by (`money`).
 */
const schemaAt = (root: AnyRecord, schema: unknown, segments: string[]): unknown => {
  let current = schema
  for (const segment of segments) {
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
  return current
}

/** `$response.body#/id` → `{ base: '$response.body', segments: ['id'] }`. */
const splitPointer = (expression: string): { base: string; segments: string[] } => {
  const hash = expression.indexOf('#')
  return hash < 0 ? { base: expression, segments: [] } : { base: expression.slice(0, hash), segments: parseJsonPointer(expression.slice(hash + 1)) }
}

/** A schema found for a value, with the document its `$ref`s resolve against. */
type Found = { schema?: unknown; root?: unknown; origin?: string }

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
const stepOutputSchema = (document: ArazzoDocument, workflow: ArazzoWorkflow, step: ArazzoStep, name: string, documents: Record<string, unknown>, depth: number): Found => {
  const expression = typeof step.outputs?.[name] === 'string' ? step.outputs[name] : undefined
  if (!expression) {
    return {}
  }
  const { base, segments } = splitPointer(expression.trim())
  const target = resolveStepTarget(document, step, documents)
  if (base === '$response.body') {
    const response = successResponse(step, operationResponses(target, documents))
    const content = response?.contents.find((candidate) => /json/i.test(candidate.contentType)) ?? response?.contents[0]
    const operation = target?.kind === 'operation' && target.method && target.path ? ` of ${target.method.toUpperCase()} ${target.path}` : ''
    return response
      ? { schema: content && schemaAt(response.root, content.schema, segments), root: response.root, origin: `${response.status} response body${operation}` }
      : { origin: 'the response body' }
  }
  if (base.startsWith('$response.header.')) {
    return { origin: `response header ${base.slice('$response.header.'.length)}` }
  }
  if (base === '$message.payload') {
    const message = targetMessages(target, documents)[0]
    return message && isRecord(message.payload)
      ? { schema: schemaAt(message.root, message.payload, segments), root: message.root, origin: `payload of message ${message.title || message.name}` }
      : { origin: 'the message payload' }
  }
  if (base.startsWith('$message.header.')) {
    return { origin: `message header ${base.slice('$message.header.'.length)}` }
  }
  // An output passed on from an input or another step's output: describe it where it came from.
  return depth < 4 ? sourceSchema(document, workflow, expression, documents, depth + 1) : {}
}

/** The schema of an input of the workflow, with the Arazzo document (`#/components/inputs/…`) and the schema (`#/$defs/…`) as root. */
const inputSchema = (document: ArazzoDocument, workflow: ArazzoWorkflow, segments: string[]): Found => {
  const root = document as unknown as AnyRecord
  const inputs = deref(root, workflow.inputs)
  return isRecord(inputs) ? { schema: schemaAt(root, inputs, segments), root: { ...root, ...inputs }, origin: "the workflow's inputs" } : { origin: "the workflow's inputs" }
}

/** Where an expression's value is made, followed back to a schema when it can be. */
const sourceSchema = (document: ArazzoDocument, workflow: ArazzoWorkflow, expression: string, documents: Record<string, unknown>, depth: number): Found => {
  const parsed = parseExpression(expression)
  if (parsed.source === 'inputs') {
    return inputSchema(document, workflow, [parsed.name, ...splitPointer(expression).segments])
  }
  if (parsed.source === 'steps' && parsed.field === 'outputs' && parsed.name) {
    const step = workflow.steps?.find((candidate) => candidate.stepId === parsed.stepId)
    const found = step ? stepOutputSchema(document, workflow, step, parsed.name, documents, depth) : {}
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
    ...Object.entries(step.outputs ?? {}).map(([name, value]) => ({ place: `output ${name}`, value })),
  ]
}

/**
 * The values a Data flow arrow carries from `producer` to `consumer`, each with where it is made (the
 * producer's output expression and the response or message it reads, or the workflow's inputs), its schema
 * as far as the loaded documents describe it, and where the consumer uses it. A value whose schema cannot be
 * found (its document isn't loaded, a header, another workflow's output) still says where it comes from.
 */
export const describeFlow = (
  document: ArazzoDocument,
  workflow: ArazzoWorkflow,
  producer: string,
  consumer: string,
  names: string[],
  documents: Record<string, unknown>,
): FlowValue[] =>
  names.map((name) => {
    const places = placesIn(document, workflow, consumer).flatMap(({ place, value }) => {
      const reading = expressionsOf(value).find((expression) => readsFrom(expression, producer, name))
      return reading ? [{ place, reading }] : []
    })
    const step = workflow.steps?.find((candidate) => candidate.stepId === producer)
    const produced = step && typeof step.outputs?.[name] === 'string' ? step.outputs[name] : undefined
    const reading = places[0]?.reading
    // Described where it is made; a reading with a pointer into it (`$steps.a.outputs.order#/id`) narrows it.
    const own = producer === INPUTS_NODE_ID
      ? `$inputs.${name}`
      : producer.startsWith('::workflow:')
        ? `$workflows.${producer.slice('::workflow:'.length)}.outputs.${name}`
        : `$steps.${producer}.outputs.${name}`
    const found = sourceSchema(document, workflow, reading?.includes('#') ? reading : own, documents, 0)
    const rows = found.schema !== undefined ? schemaPropertyRows({ type: 'object', properties: { [name]: found.schema } }, found.root) : undefined
    return {
      name,
      source: produced ?? reading ?? name,
      ...(found.origin ? { origin: found.origin } : {}),
      ...(rows?.[0] ? { schema: rows[0] } : {}),
      uses: [...new Set(places.map(({ place }) => place))],
    }
  })
