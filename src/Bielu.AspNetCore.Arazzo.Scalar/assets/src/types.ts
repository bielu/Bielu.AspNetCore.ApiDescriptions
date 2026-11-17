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

export type ArazzoCriterion = {
  condition: string
  context?: string
  type?: string | { type?: string; version?: string }
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
}

export type ArazzoFailureAction = {
  name: string
  type: 'end' | 'goto' | 'retry' | string
  workflowId?: string
  stepId?: string
  retryAfter?: number
  retryLimit?: number
  criteria?: ArazzoCriterion[]
}

export type ArazzoRequestBody = {
  contentType?: string
  payload?: unknown
  replacements?: { target: string; value: unknown }[]
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
  info?: ArazzoInfo
  sourceDescriptions?: ArazzoSourceDescription[]
  workflows?: ArazzoWorkflow[]
  components?: Record<string, Record<string, unknown> | undefined>
  /** Set by Scalar's workspace store: the URL the document was loaded from. */
  'x-scalar-original-source-url'?: string
}

/** A sidebar entry contributed by a plugin document type (`PluginNavigationEntry` in `@scalar/types`). */
export type PluginNavigationEntry = {
  id: string
  title: string
  kind?: string
  children?: PluginNavigationEntry[]
}

/** The workspace event bus subset the plugin emits on. */
export type PluginEventBus = {
  emit: (event: string, payload: { id: string }) => void
}
