import type { ArazzoDocument, ArazzoStep, ArazzoWorkflow } from '../types'

type AnyRecord = Record<string, unknown>

const isRecord = (value: unknown): value is AnyRecord => !!value && typeof value === 'object' && !Array.isArray(value)

/** Objects only: a list that holds anything else (or is not a list at all) loses those entries. */
const records = (value: unknown): AnyRecord[] | undefined => (Array.isArray(value) ? value.filter(isRecord) : undefined)

const strings = (value: unknown): string[] | undefined =>
  Array.isArray(value) ? value.filter((entry): entry is string => typeof entry === 'string') : undefined

const text = (value: unknown): string | undefined => (typeof value === 'string' ? value : undefined)

/** Criteria need a string condition; a criterion without one has nothing to show. */
const criteria = (value: unknown): AnyRecord[] | undefined =>
  records(value)?.filter((criterion) => typeof criterion.condition === 'string')

/** Actions keep their own fields, with their criteria and parameters cleaned the same way. */
const actions = (value: unknown): AnyRecord[] | undefined =>
  records(value)?.map((action) => ({ ...action, criteria: criteria(action.criteria), parameters: records(action.parameters) }))

const STEP_TEXT_FIELDS = ['description', 'operationId', 'operationPath', 'channelPath', 'workflowId', 'action', 'correlationId'] as const

const normalizeStep = (step: AnyRecord): ArazzoStep => {
  const normalized: AnyRecord = {
    ...step,
    dependsOn: strings(step.dependsOn),
    parameters: records(step.parameters),
    successCriteria: criteria(step.successCriteria),
    onSuccess: actions(step.onSuccess),
    onFailure: actions(step.onFailure),
    outputs: isRecord(step.outputs) ? step.outputs : undefined,
    requestBody: isRecord(step.requestBody)
      ? { ...step.requestBody, replacements: records(step.requestBody.replacements) }
      : undefined,
    timeout: typeof step.timeout === 'number' ? step.timeout : undefined,
  }
  for (const field of STEP_TEXT_FIELDS) {
    normalized[field] = text(step[field])
  }
  return normalized as ArazzoStep
}

const normalizeWorkflow = (workflow: AnyRecord): ArazzoWorkflow =>
  ({
    ...workflow,
    summary: text(workflow.summary),
    description: text(workflow.description),
    dependsOn: strings(workflow.dependsOn),
    parameters: records(workflow.parameters),
    successActions: actions(workflow.successActions),
    failureActions: actions(workflow.failureActions),
    outputs: isRecord(workflow.outputs) ? workflow.outputs : undefined,
    steps: records(workflow.steps)?.filter((step) => typeof step.stepId === 'string').map(normalizeStep),
  }) as ArazzoWorkflow

const cache = new WeakMap<object, ArazzoDocument>()

/**
 * A copy of an Arazzo document that the view can rely on: every list is a list of objects, workflows
 * and steps have their ids, sources their name and URL, criteria their condition, and every field
 * read as text is text. Anything else is dropped rather than rendered half-way or thrown on, since
 * documents come from user input and Scalar only checks the `arazzo` field.
 *
 * The result is cached per document object, so the sidebar (`traverse`) and the content, which pair
 * their entries by position, always see the same workflows and steps.
 */
export const normalizeArazzoDocument = (document: unknown): ArazzoDocument | undefined => {
  if (!isRecord(document)) {
    return undefined
  }
  const cached = cache.get(document)
  if (cached) {
    return cached
  }
  const info = isRecord(document.info) ? document.info : undefined
  const normalized = {
    ...document,
    arazzo: text(document.arazzo) ?? '',
    $self: text(document.$self),
    info: info && {
      ...info,
      title: text(info.title),
      summary: text(info.summary),
      description: text(info.description),
      version: text(info.version),
    },
    sourceDescriptions: records(document.sourceDescriptions)?.filter(
      (source) => typeof source.name === 'string' && typeof source.url === 'string',
    ),
    workflows: records(document.workflows)
      ?.filter((workflow) => typeof workflow.workflowId === 'string')
      .map(normalizeWorkflow),
    // Component kinds must be maps; extensions (`x-*`) are kept as they are.
    components: isRecord(document.components)
      ? Object.fromEntries(
          Object.entries(document.components).filter(([kind, value]) => kind.startsWith('x-') || isRecord(value)),
        )
      : undefined,
  } as ArazzoDocument
  cache.set(document, normalized)
  return normalized
}
