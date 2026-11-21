import type { ArazzoDocument, ArazzoParameter, ArazzoStep, ArazzoWorkflow } from '../types'
import { resolveLocalRef } from './extensions'
import { resolveReusable } from './reusable'

/**
 * Runtime Expressions (Arazzo §4.8): how a step reads the workflow's inputs, an earlier step's
 * outputs, another workflow's outputs, a component or a source description. They stand alone as a
 * value (`$steps.getPet.outputs.id`), inside a condition (`$statusCode == 200`), or embedded in a
 * string in braces (`Bearer {$inputs.token}`).
 */

/** The expression sources Arazzo defines; anything else starting with `$` (a JSONPath `$.items`) is not one. */
const EXPRESSION =
  /\$(?:url|method|statusCode|request|response|message|inputs|outputs|steps|workflows|sourceDescriptions|components|self)\b(?:[.#][^\s{}()'"`,;=<>!&|[\]]*)?/g

/** One piece of a text: plain text, or a Runtime Expression found in it. */
export type ExpressionSegment = { text: string; expression?: true }

/**
 * Splits a text into plain text and the Runtime Expressions in it, in order. Joining the segments'
 * `text` gives the text back. In prose a trailing `.` or `#` ends a sentence, not the expression; an
 * expression that is the whole value, or closes a `{…}`, keeps it, since a name may end in `.`.
 */
export const splitExpressions = (text: string): ExpressionSegment[] => {
  const segments: ExpressionSegment[] = []
  let last = 0
  for (const match of text.matchAll(EXPRESSION)) {
    const start = match.index
    const end = start + match[0].length
    const whole = !text.slice(0, start).trim() && !text.slice(end).trim()
    const expression = whole || text[end] === '}' ? match[0] : match[0].replace(/[.#]+$/, '')
    if (start > last) {
      segments.push({ text: text.slice(last, start) })
    }
    segments.push({ text: expression, expression: true })
    last = start + expression.length
  }
  if (last < text.length) {
    segments.push({ text: text.slice(last) })
  }
  return segments
}

/** The Runtime Expressions in a text, in order. */
export const expressionsIn = (text: string): string[] =>
  splitExpressions(text).flatMap((segment) => (segment.expression ? [segment.text] : []))

/** What a Runtime Expression reads, as far as the Arazzo document can tell. */
export type ParsedExpression =
  | { source: 'inputs'; name: string }
  | { source: 'outputs'; name: string }
  | { source: 'steps'; stepId: string; field?: string; name?: string }
  | { source: 'workflows'; workflowId: string; field?: string; name?: string }
  | { source: 'components'; kind: string; name?: string }
  | { source: 'sourceDescriptions'; name: string }
  | { source: 'other' }

/** A name ends where a JSON Pointer (`#/…`) into its value starts. */
const nameOf = (value: string | undefined): string | undefined => value?.split('#')[0] || undefined

/** The parts after the first `count`, joined back: a name may itself contain dots (`order.id`). */
const restOf = (parts: string[], count: number): string | undefined =>
  parts.length > count ? nameOf(parts.slice(count).join('.')) : undefined

/**
 * Reads the parts of a Runtime Expression that point into the Arazzo document. Step, workflow and
 * source description ids are `[A-Za-z0-9_\-]+` (the spec's recommendation), and so are the field
 * (`outputs`) and component kind after them, so a `.` separates those. The name that follows is
 * everything up to an optional JSON Pointer, dots included: the grammar's `name` is `*( CHAR )`.
 */
export const parseExpression = (expression: string): ParsedExpression => {
  const [head, ...parts] = expression.split('.')
  switch (head) {
    case '$inputs':
    case '$outputs': {
      const name = restOf(parts, 0)
      return name ? { source: head === '$inputs' ? 'inputs' : 'outputs', name } : { source: 'other' }
    }
    case '$steps':
    case '$workflows': {
      const id = nameOf(parts[0])
      if (!id) {
        return { source: 'other' }
      }
      const field = nameOf(parts[1])
      const name = field && !parts[1]!.includes('#') ? restOf(parts, 2) : undefined
      return head === '$steps' ? { source: 'steps', stepId: id, field, name } : { source: 'workflows', workflowId: id, field, name }
    }
    case '$components': {
      const kind = nameOf(parts[0])
      return kind ? { source: 'components', kind, name: parts[0]!.includes('#') ? undefined : restOf(parts, 1) } : { source: 'other' }
    }
    case '$sourceDescriptions': {
      const name = nameOf(parts[0])
      return name ? { source: 'sourceDescriptions', name } : { source: 'other' }
    }
    default:
      return { source: 'other' }
  }
}

/** Where on the page the expressions of one workflow (or of the components, with no workflow) lead. */
export type ExpressionScope = {
  document: ArazzoDocument
  /** The workflow the expression is written in; `$inputs` and `$steps` read from it. */
  workflow?: ArazzoWorkflow
  /** workflowId → the sidebar id of that workflow's section. */
  workflowNavIds?: Record<string, string>
  /** workflowId → stepId → section id. */
  stepSectionIds?: Record<string, Record<string, string>>
  /** The components section's sidebar id, when the document has one. */
  componentsId?: string
}

/** Where a Runtime Expression leads, or why it leads nowhere. */
export type ExpressionTarget = {
  /** The id of the element that shows what the expression reads. */
  anchor?: string
  /** The section of the step that element is in, which has to be opened first. */
  stepSection?: string
  /** The section of the workflow that element is in, outside any step: shown even while the filter hides it. */
  workflowSection?: string
  /** What the expression reads, in words, for its tooltip. */
  title: string
  /** Set when the expression points at something the document does not declare: why. */
  broken?: string
}

/** The anchor of an outputs row (`<section>/outputs/<name>`), or of the outputs block without a name. */
export const outputsAnchor = (sectionId: string, name?: string): string => `${sectionId}/outputs${name ? `/${name}` : ''}`

/** The anchor of a workflow's input property (`<workflow>/inputs/<name>`), or of its inputs block. */
export const inputsAnchor = (workflowNavId: string, name?: string): string => `${workflowNavId}/inputs${name ? `/${name}` : ''}`

/** The anchor of a component's row (`<components>/<kind>/<name>`). */
export const componentAnchor = (componentsId: string, kind: string, name: string): string => `${componentsId}/${kind}/${name}`

const has = (record: unknown, key: string): boolean =>
  !!record && typeof record === 'object' && Object.prototype.hasOwnProperty.call(record, key)

/** The property names of a workflow's inputs schema, or `undefined` when it does not list them. */
const inputNames = (document: ArazzoDocument, workflow: ArazzoWorkflow): Set<string> | undefined => {
  const schema = resolveLocalRef(document, workflow.inputs) as { properties?: unknown } | undefined
  return schema?.properties && typeof schema.properties === 'object' ? new Set(Object.keys(schema.properties)) : undefined
}

const resolveInput = (scope: ExpressionScope, workflow: ArazzoWorkflow, name: string): ExpressionTarget => {
  const navId = scope.workflowNavIds?.[workflow.workflowId]
  const title = `Input ${name} of workflow ${workflow.workflowId}`
  if (!workflow.inputs) {
    return { title, broken: `Workflow ${workflow.workflowId} declares no inputs` }
  }
  const names = inputNames(scope.document, workflow)
  if (names && !names.has(name)) {
    return { title, broken: `Workflow ${workflow.workflowId} has no input “${name}”` }
  }
  return { title, anchor: navId ? inputsAnchor(navId, names ? name : undefined) : undefined }
}

const resolveStepOutput = (scope: ExpressionScope, workflow: ArazzoWorkflow, stepId: string, field?: string, name?: string): ExpressionTarget => {
  const step = workflow.steps?.find((candidate) => candidate.stepId === stepId)
  const section = scope.stepSectionIds?.[workflow.workflowId]?.[stepId]
  if (!step) {
    return { title: `Step ${stepId}`, broken: `Workflow ${workflow.workflowId} has no step “${stepId}”` }
  }
  if (field !== 'outputs' || !name) {
    return { title: `Step ${stepId}`, anchor: section, stepSection: section }
  }
  const title = `Output ${name} of step ${stepId}`
  if (!has(step.outputs, name)) {
    return { title, broken: `Step ${stepId} has no output “${name}”` }
  }
  return { title, anchor: section ? outputsAnchor(section, name) : undefined, stepSection: section }
}

const resolveWorkflowField = (scope: ExpressionScope, workflowId: string, field?: string, name?: string): ExpressionTarget => {
  const workflow = scope.document.workflows?.find((candidate) => candidate.workflowId === workflowId)
  const navId = scope.workflowNavIds?.[workflowId]
  if (!workflow) {
    return { title: `Workflow ${workflowId}`, broken: `This document has no workflow “${workflowId}”` }
  }
  // Another workflow's rows sit outside its steps: the filter may hide that whole workflow.
  const section = navId ? { workflowSection: navId } : {}
  if (field === 'outputs' && name) {
    const title = `Output ${name} of workflow ${workflowId}`
    return has(workflow.outputs, name)
      ? { title, anchor: navId ? outputsAnchor(navId, name) : undefined, ...section }
      : { title, broken: `Workflow ${workflowId} has no output “${name}”` }
  }
  if (field === 'inputs' && name) {
    const input = resolveInput(scope, workflow, name)
    return input.broken ? input : { ...input, ...section }
  }
  return { title: `Workflow ${workflowId}`, anchor: navId, ...section }
}

/**
 * Where a Runtime Expression leads on the page: the input, step output, workflow output or component
 * it reads. An expression that reads the HTTP exchange or the message (`$statusCode`, `$response.body`)
 * leads nowhere but is not broken. One that names an input, step, output, workflow, component or
 * source the document does not declare is `broken`, with the reason.
 */
export const resolveExpression = (expression: string, scope: ExpressionScope): ExpressionTarget => {
  const parsed = parseExpression(expression)
  const workflow = scope.workflow
  switch (parsed.source) {
    case 'inputs':
      return workflow ? resolveInput(scope, workflow, parsed.name) : { title: `Input ${parsed.name}` }
    case 'outputs': {
      // `$outputs` reads the workflow's own outputs, as its `outputs` and `successActions` do.
      const navId = workflow ? scope.workflowNavIds?.[workflow.workflowId] : undefined
      return { title: `Output ${parsed.name} of the workflow`, anchor: navId && has(workflow?.outputs, parsed.name) ? outputsAnchor(navId, parsed.name) : undefined }
    }
    case 'steps':
      return workflow ? resolveStepOutput(scope, workflow, parsed.stepId, parsed.field, parsed.name) : { title: `Step ${parsed.stepId}` }
    case 'workflows':
      return resolveWorkflowField(scope, parsed.workflowId, parsed.field, parsed.name)
    case 'components': {
      if (!parsed.name) {
        return { title: `Components: ${parsed.kind}` }
      }
      const title = `Component ${parsed.kind}.${parsed.name}`
      if (!has(scope.document.components?.[parsed.kind], parsed.name)) {
        return { title, broken: `This document has no ${parsed.kind} component “${parsed.name}”` }
      }
      return { title, anchor: scope.componentsId ? componentAnchor(scope.componentsId, parsed.kind, parsed.name) : undefined }
    }
    case 'sourceDescriptions': {
      const title = `Source description ${parsed.name}`
      return scope.document.sourceDescriptions?.some((source) => source.name === parsed.name)
        ? { title }
        : { title, broken: `This document has no source description “${parsed.name}”` }
    }
    default:
      return { title: 'Runtime expression' }
  }
}

/** Fields of Arazzo objects that are prose, not values: a `$inputs.x` mentioned in a description is not read. */
const PROSE = new Set(['description', 'summary', 'title'])

/** Fields of Arazzo objects whose value is the API's data (a parameter's value, a request payload), any shape. */
const DATA = new Set(['payload', 'value'])

/**
 * Every string inside a value. On Arazzo objects (`metadata`), prose fields are skipped; inside data
 * (a payload, a parameter value, an outputs map) every key is the API's or the author's own, so a
 * `title` there is read like any other field.
 */
const stringsIn = (value: unknown, out: string[], metadata = true): string[] => {
  if (typeof value === 'string') {
    out.push(value)
  } else if (Array.isArray(value)) {
    value.forEach((item) => stringsIn(item, out, metadata))
  } else if (value && typeof value === 'object') {
    for (const [key, item] of Object.entries(value)) {
      if (!key.startsWith('x-scalar-') && !(metadata && PROSE.has(key))) {
        stringsIn(item, out, metadata && !DATA.has(key))
      }
    }
  }
  return out
}

/** A list that may hold Reusable Objects, with each one followed to its component (kept as well, for its reference). */
const withReusables = (document: ArazzoDocument, entries: unknown[] | undefined): unknown[] =>
  (entries ?? []).flatMap((entry) => {
    const resolved = resolveReusable<object>(document, entry as ArazzoParameter)
    return resolved && resolved !== entry ? [entry, resolved] : [entry]
  })

/** A list of actions that may hold Reusable Objects, followed, with the parameter components each action passes followed too. */
const withActions = (document: ArazzoDocument, entries: unknown[] | undefined): unknown[] =>
  withReusables(document, entries).flatMap((action) => {
    const parameters = (action as { parameters?: unknown }).parameters
    // The action itself still holds each parameter's reference; add only the components they resolve to.
    const resolved = Array.isArray(parameters) ? withReusables(document, parameters).filter((parameter) => !parameters.includes(parameter)) : []
    return [action, ...resolved]
  })

/**
 * The Runtime Expressions a step reads: in its parameters (components followed), request body,
 * success criteria, actions, outputs and `correlationId` (the value a received message is matched on).
 * Its `stepId`, target and prose fields are not read.
 */
export const stepExpressions = (document: ArazzoDocument, step: ArazzoStep): string[] => {
  const strings = stringsIn(
    [
      withReusables(document, step.parameters),
      step.requestBody,
      step.successCriteria,
      withActions(document, step.onSuccess),
      withActions(document, step.onFailure),
    ],
    [],
  )
  stringsIn([step.outputs, step.correlationId], strings, false)
  return strings.flatMap(expressionsIn)
}

/** A part of a workflow, outside its steps, that reads Runtime Expressions. */
type WorkflowBlock = 'parameters' | 'actions' | 'outputs'

/** The Runtime Expressions a workflow reads outside its steps, by the block that reads them. */
const workflowReads = (document: ArazzoDocument, workflow: ArazzoWorkflow): [WorkflowBlock, string[]][] => [
  ['parameters', stringsIn(withReusables(document, workflow.parameters), []).flatMap(expressionsIn)],
  ['actions', stringsIn([withActions(document, workflow.successActions), withActions(document, workflow.failureActions)], []).flatMap(expressionsIn)],
  ['outputs', stringsIn(workflow.outputs, [], false).flatMap(expressionsIn)],
]

/** The Runtime Expressions a workflow reads outside its steps: its parameters, actions and outputs. */
export const workflowExpressions = (document: ArazzoDocument, workflow: ArazzoWorkflow): string[] =>
  workflowReads(document, workflow).flatMap(([, expressions]) => expressions)

/**
 * Where a link to a workflow block lands: the outputs block has an anchor of its own; parameters and
 * actions land on the workflow's section.
 */
const blockConsumer = (label: string, block: WorkflowBlock, navId: string | undefined): OutputConsumer =>
  navId ? { label, anchor: block === 'outputs' ? outputsAnchor(navId) : navId, workflowSection: navId } : { label }

/** Something that reads an output: a step (with its section), or a workflow's parameters, actions or outputs. */
export type OutputConsumer = { label: string; anchor?: string; stepSection?: string; workflowSection?: string }

/**
 * For each output of a step: the steps of its workflow that read it (`$steps.<id>.outputs.<name>`)
 * and whether the workflow's own outputs or actions do, in step order.
 */
export const stepOutputConsumers = (
  scope: ExpressionScope & { workflow: ArazzoWorkflow },
  stepId: string,
): Record<string, OutputConsumer[]> => {
  // Prototype-free, so an output named `constructor` or `toString` starts with no consumers.
  const consumers: Record<string, OutputConsumer[]> = Object.create(null)
  const add = (name: string, consumer: OutputConsumer) => {
    const list = (consumers[name] ??= [])
    if (!list.some((entry) => entry.label === consumer.label)) {
      list.push(consumer)
    }
  }
  const readsOf = (expressions: string[]) =>
    expressions.flatMap((expression) => {
      const parsed = parseExpression(expression)
      return parsed.source === 'steps' && parsed.stepId === stepId && parsed.field === 'outputs' && parsed.name ? [parsed.name] : []
    })
  const sections = scope.stepSectionIds?.[scope.workflow.workflowId] ?? {}
  for (const step of scope.workflow.steps ?? []) {
    if (step.stepId === stepId) {
      continue
    }
    for (const name of readsOf(stepExpressions(scope.document, step))) {
      const section = sections[step.stepId]
      add(name, { label: step.stepId, anchor: section, stepSection: section })
    }
  }
  const navId = scope.workflowNavIds?.[scope.workflow.workflowId]
  for (const [block, expressions] of workflowReads(scope.document, scope.workflow)) {
    for (const name of readsOf(expressions)) {
      add(name, blockConsumer(`workflow ${block}`, block, navId))
    }
  }
  return consumers
}

/**
 * For each output of a workflow: what in the document's other workflows reads it
 * (`$workflows.<id>.outputs.<name>`): a step, labelled `workflow / step`, or a workflow's own
 * parameters, actions or outputs, labelled `workflow / outputs`.
 */
export const workflowOutputConsumers = (scope: ExpressionScope & { workflow: ArazzoWorkflow }): Record<string, OutputConsumer[]> => {
  const consumers: Record<string, OutputConsumer[]> = Object.create(null)
  const workflowId = scope.workflow.workflowId
  const add = (expressions: string[], consumer: () => OutputConsumer) => {
    for (const expression of expressions) {
      const parsed = parseExpression(expression)
      if (parsed.source !== 'workflows' || parsed.workflowId !== workflowId || parsed.field !== 'outputs' || !parsed.name) {
        continue
      }
      const entry = consumer()
      const list = (consumers[parsed.name] ??= [])
      if (!list.some((existing) => existing.label === entry.label)) {
        list.push(entry)
      }
    }
  }
  for (const other of scope.document.workflows ?? []) {
    const navId = scope.workflowNavIds?.[other.workflowId]
    for (const step of other.steps ?? []) {
      add(stepExpressions(scope.document, step), () => {
        const section = scope.stepSectionIds?.[other.workflowId]?.[step.stepId]
        return { label: `${other.workflowId} / ${step.stepId}`, anchor: section, stepSection: section }
      })
    }
    for (const [block, expressions] of workflowReads(scope.document, other)) {
      add(expressions, () => blockConsumer(`${other.workflowId} / ${block}`, block, navId))
    }
  }
  return consumers
}
