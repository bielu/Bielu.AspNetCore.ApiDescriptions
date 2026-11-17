/**
 * The subset of the Arazzo 1.x object model the plugin renders. Declared locally (rather than taken
 * from `@scalar/types/arazzo`) so the bundle does not depend on a Scalar version that ships them.
 * Every field the spec makes optional is optional here too: documents come from user input.
 */

/** A `{ reference, value }` Reusable Object pointing into `components` (`$components.<kind>.<name>`). */
export type ArazzoReusableObject = {
  reference: string
  value?: unknown
}

export type ArazzoInfo = {
  title?: string
  summary?: string
  description?: string
  version?: string
}

export type ArazzoSourceDescription = {
  name: string
  url: string
  type?: string
}

/** How a criterion's condition is read: `simple` (the default), `regex`, `jsonpath` or `xpath`. */
export type ArazzoCriterionExpressionType = { type?: string; version?: string }

export type ArazzoCriterion = {
  condition: string
  context?: string
  type?: string | ArazzoCriterionExpressionType
}

export type ArazzoParameter = {
  name: string
  in?: string
  value?: unknown
}

export type ArazzoSuccessAction = {
  name: string
  type: 'end' | 'goto' | string
  workflowId?: string
  stepId?: string
  criteria?: ArazzoCriterion[]
  /** Parameters for the workflow a `goto` action invokes. */
  parameters?: (ArazzoParameter | ArazzoReusableObject)[]
}

export type ArazzoFailureAction = {
  name: string
  type: 'end' | 'goto' | 'retry' | string
  workflowId?: string
  stepId?: string
  retryAfter?: number
  retryLimit?: number
  criteria?: ArazzoCriterion[]
  /** Parameters for the workflow a `goto` or `retry` action invokes. */
  parameters?: (ArazzoParameter | ArazzoReusableObject)[]
}

/**
 * A Selector Object (Arazzo 1.1): picks a value out of a Runtime Expression's result with JSONPath,
 * XPath or a JSON Pointer. It can stand wherever a value can: outputs, parameter values, replacements.
 */
export type ArazzoSelector = {
  context: string
  selector: string
  type: string | ArazzoCriterionExpressionType
}

/** Replaces one location of a request body payload before the step runs. */
export type ArazzoPayloadReplacement = {
  /** A JSON Pointer, or (Arazzo 1.1) a JSONPath or XPath expression, into the payload. */
  target: string
  /** How `target` is written, when it is not a JSON Pointer (Arazzo 1.1). */
  targetSelectorType?: string | ArazzoCriterionExpressionType
  value: unknown
}

export type ArazzoRequestBody = {
  contentType?: string
  payload?: unknown
  replacements?: ArazzoPayloadReplacement[]
}

export type ArazzoStep = {
  stepId: string
  description?: string
  operationId?: string
  operationPath?: string
  /** AsyncAPI channel target (Arazzo 1.1). */
  channelPath?: string
  /** What the step does on its channel target: `send` or `receive` (Arazzo 1.1). */
  action?: string
  /** How long to wait for the step, in milliseconds, before it fails (Arazzo 1.1). */
  timeout?: number
  /** Links a received message to the request it answers (Arazzo 1.1, `receive` steps). */
  correlationId?: string
  workflowId?: string
  dependsOn?: string[]
  parameters?: (ArazzoParameter | ArazzoReusableObject)[]
  requestBody?: ArazzoRequestBody
  successCriteria?: ArazzoCriterion[]
  onSuccess?: (ArazzoSuccessAction | ArazzoReusableObject)[]
  onFailure?: (ArazzoFailureAction | ArazzoReusableObject)[]
  outputs?: Record<string, unknown>
}

export type ArazzoWorkflow = {
  workflowId: string
  summary?: string
  description?: string
  inputs?: Record<string, unknown>
  dependsOn?: string[]
  steps?: ArazzoStep[]
  successActions?: (ArazzoSuccessAction | ArazzoReusableObject)[]
  failureActions?: (ArazzoFailureAction | ArazzoReusableObject)[]
  outputs?: Record<string, unknown>
  parameters?: (ArazzoParameter | ArazzoReusableObject)[]
}

export type ArazzoDocument = {
  arazzo: string
  /** The document's own canonical URL, the base its relative references resolve against (Arazzo 1.1). */
  $self?: string
  info?: ArazzoInfo
  sourceDescriptions?: ArazzoSourceDescription[]
  workflows?: ArazzoWorkflow[]
  components?: Record<string, Record<string, unknown> | undefined>
  /** Set by Scalar's workspace store: the URL the document was loaded from. */
  'x-scalar-original-source-url'?: string
}

/**
 * The reusable `components`, by kind (`inputs`, `parameters`, `successActions`, `failureActions`)
 * and then by name. Kept loose: entries are resolved where they are used.
 */
export type ArazzoComponents = NonNullable<ArazzoDocument['components']>

/** A sidebar entry contributed by a plugin document type (`PluginNavigationEntry` in `@scalar/types`). */
export type PluginNavigationEntry = {
  id: string
  title: string
  kind?: string
  children?: PluginNavigationEntry[]
}

/** The workspace event bus subset the plugin emits on. */
export type PluginEventBus = {
  emit: (event: string, payload: { id: string; open?: boolean }) => void
}
