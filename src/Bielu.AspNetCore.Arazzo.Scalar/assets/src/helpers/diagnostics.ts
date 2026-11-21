import type { ArazzoDocument, ArazzoParameter, ArazzoStep, ArazzoWorkflow } from '../types'
import { effectiveSuccessActions, isUnconditional, reachableActions, effectiveFailureActions } from './actions'
import { parseExpression, resolveExpression, stepExpressions, workflowExpressions, type ExpressionScope } from './expressions'
import { operationResponses, statusCodesIn } from './openapi-exchange'
import { normalizeArazzoDocument } from './normalize'
import { findDocumentByUrl, QUALIFIED_WORKFLOW, resolveStepTarget, sourceNameOf, targetParameters, type StepTarget } from './resolve-target'
import { resolveReusables } from './reusable'

/**
 * Problems in an Arazzo document the view can see on its own, without running anything: references to
 * things that are not there, parameters the target doesn't take, statuses it doesn't return, steps that can
 * never run. Errors are certainly wrong; warnings are likely mistakes.
 */
export type Diagnostic = {
  severity: 'error' | 'warning'
  message: string
  workflowId: string
  /** The step it is about; workflow-level problems have none. */
  stepId?: string
  /** Where on the page it is: the step's section, or the workflow's. */
  anchor?: string
}

/** Where the checks find the loaded documents and the page's section ids. */
export type DiagnosticContext = {
  documents: Record<string, unknown>
  workflowNavIds?: Record<string, string>
  stepSectionIds?: Record<string, Record<string, string>>
  componentsId?: string
}

/** The source description a target names, and whether its document is loaded (`undefined` when none is named). */
const namedSourceLoaded = (document: ArazzoDocument, step: ArazzoStep, documents: Record<string, unknown>): boolean | undefined => {
  const reference = step.operationPath ?? step.channelPath ?? step.operationId
  const name = reference ? sourceNameOf(reference) : undefined
  if (!name) {
    return undefined
  }
  const source = document.sourceDescriptions?.find((candidate) => candidate.name === name)
  return !!source && !!findDocumentByUrl(document, documents, source)
}

/** Whether every OpenAPI and AsyncAPI source of the document is loaded, so an unqualified `operationId` that resolves nowhere is missing. */
const apiSourcesLoaded = (document: ArazzoDocument, documents: Record<string, unknown>): boolean => {
  const sources = (document.sourceDescriptions ?? []).filter((source) => source.type !== 'arazzo')
  return sources.length > 0 && sources.every((source) => !!findDocumentByUrl(document, documents, source))
}

/**
 * The workflows of the Arazzo document a source description names: `undefined` while it is not loaded
 * (nothing can be said yet), `null` when the document has no such source description.
 */
const sourceWorkflows = (document: ArazzoDocument, name: string, documents: Record<string, unknown>): ArazzoWorkflow[] | null | undefined => {
  const source = document.sourceDescriptions?.find((candidate) => candidate.name === name)
  if (!source) {
    return null
  }
  const loaded = findDocumentByUrl(document, documents, source)
  return loaded ? (normalizeArazzoDocument(loaded.document)?.workflows ?? []) : undefined
}

/** What is wrong with a `$sourceDescriptions.<name>.<workflowId>` reference, when its source says so; `what` begins the message. */
const checkQualifiedWorkflow = (document: ArazzoDocument, what: string, reference: string, documents: Record<string, unknown>): string | undefined => {
  const [, name, workflowId] = QUALIFIED_WORKFLOW.exec(reference)!
  const workflows = sourceWorkflows(document, name!, documents)
  if (workflows === null) {
    return `${what} ${reference}, but there is no source description “${name}”`
  }
  return workflows && !workflows.some((workflow) => workflow.workflowId === workflowId)
    ? `${what} ${reference}, which the loaded source “${name}” doesn't declare`
    : undefined
}

/** `$workflows.<workflowId>.steps.<stepId>`: a step of another workflow of this document. */
const WORKFLOW_STEP = /^\$workflows\.([^.]+)\.steps\.(.+)$/

/** `$sourceDescriptions.<name>.<workflowId>.steps.<stepId>`: a step of a workflow in another Arazzo document. */
const SOURCE_STEP = /^\$sourceDescriptions\.([^.]+)\.([^.]+)\.steps\.(.+)$/

/**
 * What is wrong with a step dependency (Arazzo 1.1 §5.8.5.1): a `stepId` of the current workflow, a
 * `$workflows.<workflowId>.steps.<stepId>` of this document, or a `$sourceDescriptions.<name>.<workflowId>.steps.<stepId>`
 * of another Arazzo document, checked only once that document is loaded.
 */
const checkDependency = (document: ArazzoDocument, steps: ArazzoStep[], dependency: string, documents: Record<string, unknown>): string | undefined => {
  const local = WORKFLOW_STEP.exec(dependency)
  const remote = SOURCE_STEP.exec(dependency)
  if (!local && !remote) {
    return steps.some((candidate) => candidate.stepId === dependency) ? undefined : `Depends on step “${dependency}”, which this workflow doesn't have`
  }
  if (local) {
    const [, workflowId, stepId] = local
    const workflow = document.workflows?.find((candidate) => candidate.workflowId === workflowId)
    if (!workflow) {
      return `Depends on ${dependency}, but this document doesn't declare workflow “${workflowId}”`
    }
    return workflow.steps?.some((candidate) => candidate.stepId === stepId) ? undefined : `Depends on ${dependency}, but workflow ${workflowId} has no step “${stepId}”`
  }
  const [, name, workflowId, stepId] = remote!
  const workflows = sourceWorkflows(document, name!, documents)
  if (workflows === null) {
    return `Depends on ${dependency}, but there is no source description “${name}”`
  }
  if (!workflows) {
    return undefined
  }
  const workflow = workflows.find((candidate) => candidate.workflowId === workflowId)
  if (!workflow) {
    return `Depends on ${dependency}, but the loaded source “${name}” doesn't declare workflow “${workflowId}”`
  }
  return workflow.steps?.some((candidate) => candidate.stepId === stepId) ? undefined : `Depends on ${dependency}, but workflow ${workflowId} of source “${name}” has no step “${stepId}”`
}

const checkTarget = (document: ArazzoDocument, step: ArazzoStep, target: StepTarget | undefined, documents: Record<string, unknown>): string[] => {
  if (step.workflowId) {
    if (QUALIFIED_WORKFLOW.test(step.workflowId)) {
      const problem = checkQualifiedWorkflow(document, 'Invokes workflow', step.workflowId, documents)
      return problem ? [problem] : []
    }
    return document.workflows?.some((workflow) => workflow.workflowId === step.workflowId) ? [] : [`Invokes workflow “${step.workflowId}”, which this document doesn't declare`]
  }
  const reference = step.operationPath ?? step.channelPath ?? step.operationId
  if (!reference || (target && target.kind !== 'workflow' && target.documentName)) {
    return []
  }
  const name = sourceNameOf(reference)
  if (name && !document.sourceDescriptions?.some((source) => source.name === name)) {
    return [`Targets ${reference}, but there is no source description “${name}”`]
  }
  const loaded = namedSourceLoaded(document, step, documents) ?? apiSourcesLoaded(document, documents)
  return loaded ? [`Targets ${reference}, which the loaded ${name ? `source “${name}”` : 'source descriptions'} don't declare`] : []
}

/** The step's parameters, with the workflow's (which apply to every step) under them, Reusable Objects resolved. */
const stepParameters = (document: ArazzoDocument, workflow: ArazzoWorkflow, step: ArazzoStep): ArazzoParameter[] => [
  ...resolveReusables<ArazzoParameter>(document, step.parameters),
  ...resolveReusables<ArazzoParameter>(document, workflow.parameters),
]

const checkParameters = (document: ArazzoDocument, workflow: ArazzoWorkflow, step: ArazzoStep, target: StepTarget | undefined, documents: Record<string, unknown>) => {
  if (target?.kind !== 'operation' || !target.documentName) {
    return { errors: [] as string[], warnings: [] as string[] }
  }
  const declared = targetParameters(target, documents)
  const own = resolveReusables<ArazzoParameter>(document, step.parameters)
  const supplied = stepParameters(document, workflow, step)
  const warnings: string[] = []
  const errors: string[] = []
  for (const parameter of own) {
    const known = declared.some((candidate) => candidate.name === parameter.name && (!parameter.in || candidate.location === parameter.in))
    // Headers are often sent without being described (tracing, tenancy); only the others are checked.
    if (!known && parameter.in !== 'header') {
      warnings.push(`Passes ${parameter.in ? `${parameter.in} ` : ''}parameter “${parameter.name}”, which the operation doesn't declare`)
    }
  }
  for (const parameter of declared) {
    if (!parameter.required) {
      continue
    }
    const given = supplied.some((candidate) => candidate.name === parameter.name && (!candidate.in || candidate.in === parameter.location))
    if (!given) {
      ;(parameter.location === 'path' ? errors : warnings).push(`Doesn't pass the required ${parameter.location} parameter “${parameter.name}”`)
    }
  }
  return { errors, warnings }
}

/** Status codes the step's criteria check for that the operation declares no response for (and no range or `default` covers). */
const checkStatuses = (step: ArazzoStep, target: StepTarget | undefined, documents: Record<string, unknown>, actions: { name: string; criteria?: ArazzoStep['successCriteria'] }[]): string[] => {
  const responses = operationResponses(target, documents)
  if (!responses.length || responses.some((response) => response.status === 'default')) {
    return []
  }
  const covered = (code: string) => responses.some((response) => response.status === code || (/^\dXX$/i.test(response.status) && response.status[0] === code[0]))
  const declared = responses.map((response) => response.status).join(', ')
  const checks = [
    ...statusCodesIn(step.successCriteria).map((code) => ({ code, by: 'Its success criteria check' })),
    ...actions.flatMap((action) => statusCodesIn(action.criteria).map((code) => ({ code, by: `Action ${action.name} checks` }))),
  ]
  const seen = new Set<string>()
  return checks.flatMap(({ code, by }) => {
    const key = `${by}:${code}`
    if (covered(code) || seen.has(key)) {
      return []
    }
    seen.add(key)
    return [`${by} for status ${code}, but the operation only declares ${declared}`]
  })
}

/** The steps that certainly ran before `step`: earlier in document order, or (with `dependsOn`) its prerequisites, transitively. */
const stepsBefore = (workflow: ArazzoWorkflow, step: ArazzoStep): Set<string> => {
  const steps = workflow.steps ?? []
  if (!steps.some((candidate) => candidate.dependsOn?.length)) {
    return new Set(steps.slice(0, steps.indexOf(step)).map((candidate) => candidate.stepId))
  }
  const before = new Set<string>()
  const queue = [...(step.dependsOn ?? [])]
  for (let next = queue.shift(); next !== undefined; next = queue.shift()) {
    if (!before.has(next)) {
      before.add(next)
      queue.push(...(steps.find((candidate) => candidate.stepId === next)?.dependsOn ?? []))
    }
  }
  return before
}

/**
 * Checks one Arazzo document. Each problem is reported once, against the step (or workflow) it is in, with the
 * anchor of that step's section so the list can link to it. Targets are only checked against documents that are
 * loaded: a step whose source isn't loaded is not reported as missing.
 */
export const diagnose = (document: ArazzoDocument, context: DiagnosticContext): Diagnostic[] => {
  const diagnostics: Diagnostic[] = []
  for (const workflow of document.workflows ?? []) {
    const workflowAnchor = context.workflowNavIds?.[workflow.workflowId]
    const add = (severity: Diagnostic['severity'], message: string, stepId?: string) =>
      diagnostics.push({
        severity,
        message,
        workflowId: workflow.workflowId,
        ...(stepId ? { stepId } : {}),
        ...((stepId ? context.stepSectionIds?.[workflow.workflowId]?.[stepId] : workflowAnchor) ? { anchor: (stepId ? context.stepSectionIds?.[workflow.workflowId]?.[stepId] : workflowAnchor)! } : {}),
      })
    const scope: ExpressionScope = { document, workflow, workflowNavIds: context.workflowNavIds, stepSectionIds: context.stepSectionIds, componentsId: context.componentsId }
    const steps = workflow.steps ?? []
    const stepIds = steps.map((step) => step.stepId)

    for (const duplicate of new Set(stepIds.filter((id, index) => stepIds.indexOf(id) !== index))) {
      add('error', `Step id “${duplicate}” is used by more than one step`, duplicate)
    }

    for (const step of steps) {
      const target = resolveStepTarget(document, step, context.documents)
      checkTarget(document, step, target, context.documents).forEach((message) => add('error', message, step.stepId))

      const parameters = checkParameters(document, workflow, step, target, context.documents)
      parameters.errors.forEach((message) => add('error', message, step.stepId))
      parameters.warnings.forEach((message) => add('warning', message, step.stepId))

      const successActions = reachableActions(effectiveSuccessActions(document, workflow, step))
      const failureActions = reachableActions(effectiveFailureActions(document, workflow, step))
      checkStatuses(step, target, context.documents, [...successActions, ...failureActions]).forEach((message) => add('warning', message, step.stepId))

      for (const action of [...successActions, ...failureActions]) {
        if (action.stepId && !stepIds.includes(action.stepId)) {
          add('error', `Action ${action.name} goes to step “${action.stepId}”, which this workflow doesn't have`, step.stepId)
        }
        if (action.workflowId && QUALIFIED_WORKFLOW.test(action.workflowId)) {
          const problem = checkQualifiedWorkflow(document, `Action ${action.name} goes to workflow`, action.workflowId, context.documents)
          if (problem) {
            add('error', problem, step.stepId)
          }
        } else if (action.workflowId && !document.workflows?.some((candidate) => candidate.workflowId === action.workflowId)) {
          add('error', `Action ${action.name} goes to workflow “${action.workflowId}”, which this document doesn't declare`, step.stepId)
        }
      }

      for (const dependency of step.dependsOn ?? []) {
        const problem = checkDependency(document, steps, dependency, context.documents)
        if (problem) {
          add('error', problem, step.stepId)
          continue
        }
        // Only a step of this workflow can end it before this step runs.
        const prerequisite = steps.find((candidate) => candidate.stepId === dependency)
        if (!prerequisite) {
          continue
        }
        const ending = reachableActions(effectiveSuccessActions(document, workflow, prerequisite)).find(
          (action) => isUnconditional(action) && action.type === 'end',
        )
        if (ending) {
          add('warning', `Depends on ${dependency}, but ${dependency}'s success action ${ending.name} always ends the workflow, so this step never runs`, step.stepId)
        }
      }

      const before = stepsBefore(workflow, step)
      // What the step sends is evaluated before it runs; its outputs, criteria and actions after, so only the
      // former cannot read the step's own outputs.
      const sent = new Set(stepExpressions(document, { stepId: step.stepId, parameters: step.parameters, requestBody: step.requestBody }))
      const seen = new Set<string>()
      for (const expression of stepExpressions(document, step)) {
        if (seen.has(expression)) {
          continue
        }
        seen.add(expression)
        const resolved = resolveExpression(expression, scope)
        if (resolved.broken) {
          add('error', `${expression}: ${resolved.broken}`, step.stepId)
          continue
        }
        const parsed = parseExpression(expression)
        if (parsed.source !== 'steps' || !stepIds.includes(parsed.stepId) || before.has(parsed.stepId)) {
          continue
        }
        if (parsed.stepId !== step.stepId) {
          add('warning', `${expression} reads step ${parsed.stepId}, which doesn't run before this one`, step.stepId)
        } else if (sent.has(expression)) {
          add('warning', `${expression} sends this step's own output, which only exists once it has run`, step.stepId)
        }
      }
    }

    const seen = new Set<string>()
    for (const expression of workflowExpressions(document, workflow)) {
      const broken = seen.has(expression) ? undefined : resolveExpression(expression, scope).broken
      seen.add(expression)
      if (broken) {
        add('error', `${expression}: ${broken}`)
      }
    }
  }
  return diagnostics
}

/** "2 errors · 1 warning", or `undefined` when there is nothing to report. */
export const diagnosticSummary = (diagnostics: Diagnostic[]): string | undefined => {
  const errors = diagnostics.filter((diagnostic) => diagnostic.severity === 'error').length
  const warnings = diagnostics.length - errors
  const parts = [errors ? `${errors} ${errors === 1 ? 'error' : 'errors'}` : '', warnings ? `${warnings} ${warnings === 1 ? 'warning' : 'warnings'}` : '']
  const text = parts.filter(Boolean).join(' · ')
  return text || undefined
}
