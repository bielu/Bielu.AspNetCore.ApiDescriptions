import type { ArazzoDocument, ArazzoFailureAction, ArazzoStep, ArazzoSuccessAction, ArazzoWorkflow } from '../types'
import { resolveReusables } from './reusable'

/**
 * A step's own actions first, then the workflow's actions it doesn't override. Arazzo applies a workflow's
 * `successActions`/`failureActions` to every step; a step can override one by declaring an action with the
 * same name, but cannot remove it.
 */
const merge = <T extends { name?: string }>(own: T[], inherited: T[]): T[] => {
  const overridden = new Set(own.map((action) => action.name).filter(Boolean))
  return [...own, ...inherited.filter((action) => !action.name || !overridden.has(action.name))]
}

/** The success actions that apply to a step: its `onSuccess` plus its workflow's `successActions`, references resolved. */
export const effectiveSuccessActions = (
  document: ArazzoDocument,
  workflow: ArazzoWorkflow | undefined,
  step: ArazzoStep,
): ArazzoSuccessAction[] =>
  merge(
    resolveReusables<ArazzoSuccessAction>(document, step.onSuccess),
    resolveReusables<ArazzoSuccessAction>(document, workflow?.successActions),
  )

/** The failure actions that apply to a step: its `onFailure` plus its workflow's `failureActions`, references resolved. */
export const effectiveFailureActions = (
  document: ArazzoDocument,
  workflow: ArazzoWorkflow | undefined,
  step: ArazzoStep,
): ArazzoFailureAction[] =>
  merge(
    resolveReusables<ArazzoFailureAction>(document, step.onFailure),
    resolveReusables<ArazzoFailureAction>(document, workflow?.failureActions),
  )

/** An action without criteria always applies: nothing after it in document order is ever reached. */
export const isUnconditional = (action: ArazzoSuccessAction | ArazzoFailureAction): boolean => !action.criteria?.length

/**
 * The actions that can run: Arazzo runs the first one whose criteria hold, so the actions after the first one
 * without criteria never do. The conditional actions before it are kept.
 */
export const reachableActions = <T extends ArazzoSuccessAction | ArazzoFailureAction>(actions: T[]): T[] => {
  const first = actions.findIndex(isUnconditional)
  return first < 0 ? actions : actions.slice(0, first + 1)
}
