import type { ArazzoDocument, ArazzoStep, ArazzoWorkflow } from '../types'
import { effectiveSuccessActions, isUnconditional, reachableActions } from './actions'
import { parseExpression, stepExpressions } from './expressions'

/**
 * The order steps run in, shared by every view that reasons about it (Diagnostics, Sequence), so they agree.
 *
 * Without `dependsOn` a workflow runs sequentially, in document order. With it, a step runs once its
 * prerequisites have: the steps it lists in `dependsOn` and, as Arazzo 1.1 §5.8.5.2 tells tools to treat them,
 * the steps whose outputs it reads.
 */

/** Whether the workflow's steps declare their own order (`dependsOn`) rather than run in document order. */
export const usesDependsOn = (steps: ArazzoStep[]): boolean => steps.some((step) => step.dependsOn?.length)

/** The other steps whose outputs a step reads (`$steps.<id>.outputs.…`): its implicit dependencies. */
export const outputsRead = (document: ArazzoDocument, step: ArazzoStep): string[] =>
  stepExpressions(document, step).flatMap((expression) => {
    const parsed = parseExpression(expression)
    return parsed.source === 'steps' && parsed.field === 'outputs' && parsed.stepId !== step.stepId ? [parsed.stepId] : []
  })

/** A step's direct prerequisites when `dependsOn` is in use: what it lists and what it reads. */
export const prerequisitesOf = (document: ArazzoDocument, step: ArazzoStep): string[] => [
  ...new Set([...(step.dependsOn ?? []), ...outputsRead(document, step)]),
]

/**
 * The steps that certainly ran before `step`: earlier in document order, or (with `dependsOn`) its
 * prerequisites, transitively. A step that waits on itself through a cycle is in its own set.
 */
export const stepsBefore = (document: ArazzoDocument, workflow: ArazzoWorkflow, step: ArazzoStep): Set<string> => {
  const steps = workflow.steps ?? []
  if (!usesDependsOn(steps)) {
    return new Set(steps.slice(0, steps.indexOf(step)).map((candidate) => candidate.stepId))
  }
  const before = new Set<string>()
  const queue = prerequisitesOf(document, step)
  for (let next = queue.shift(); next !== undefined; next = queue.shift()) {
    const prerequisite = steps.find((candidate) => candidate.stepId === next)
    if (prerequisite && !before.has(next)) {
      before.add(next)
      queue.push(...prerequisitesOf(document, prerequisite))
    }
  }
  return before
}

/** The steps in the order they run; a step waits for its prerequisites, and document order breaks ties and cycles. */
export const runOrder = (steps: ArazzoStep[], document: ArazzoDocument = { arazzo: '1.1.0' }): ArazzoStep[] => {
  if (!usesDependsOn(steps)) {
    return steps
  }
  const prerequisites = new Map(steps.map((step) => [step, prerequisitesOf(document, step)]))
  const placed = new Set<string>()
  const ordered: ArazzoStep[] = []
  const remaining = [...steps]
  while (remaining.length) {
    const index = remaining.findIndex((step) =>
      prerequisites.get(step)!.every((dependency) => placed.has(dependency) || !steps.some((other) => other.stepId === dependency)),
    )
    // A cycle: take the next step in document order rather than stop.
    const [next] = remaining.splice(index < 0 ? 0 : index, 1)
    ordered.push(next!)
    placed.add(next!.stepId)
  }
  return ordered
}

/**
 * The steps a successful run can reach, in run order. After a step, the first success action whose criteria hold
 * runs: an `end` (or a `goto` to another workflow) leaves this workflow, a `goto` to a step jumps there, and with
 * no action that always applies the run may also carry on to the next step. A step only reached through a
 * failure action is left out.
 */
export const successPath = (document: ArazzoDocument, workflow: ArazzoWorkflow): ArazzoStep[] => {
  const ordered = runOrder(workflow.steps ?? [], document)
  const reached = new Set<ArazzoStep>()
  const queue = ordered.slice(0, 1)
  for (let step = queue.shift(); step !== undefined; step = queue.shift()) {
    if (reached.has(step)) {
      continue
    }
    reached.add(step)
    const actions = reachableActions(effectiveSuccessActions(document, workflow, step))
    for (const action of actions) {
      const target = action.type === 'goto' && action.stepId ? ordered.find((candidate) => candidate.stepId === action.stepId) : undefined
      if (target) {
        queue.push(target)
      }
    }
    const next = ordered[ordered.indexOf(step) + 1]
    if (next && !actions.some(isUnconditional)) {
      queue.push(next)
    }
  }
  return ordered.filter((step) => reached.has(step))
}
