import type { WorkflowView } from './navigation'
import type { ArazzoStep, ArazzoWorkflow } from '../types'

/** What a filter leaves on screen: the workflows with a match, and within them the matching steps. */
export type WorkflowFilter = {
  /** Sidebar ids of the workflows to show. */
  workflows: Set<string>
  /** Sidebar ids of the steps to show. */
  steps: Set<string>
  /** How many steps match, for "3 of 12 steps". */
  matchingSteps: number
  totalSteps: number
}

const workflowText = (workflow: ArazzoWorkflow): string =>
  [workflow.workflowId, workflow.summary, workflow.description].filter(Boolean).join('\n').toLowerCase()

/** The fields a reader would search a step by: its id and description, and what it targets. */
const stepText = (step: ArazzoStep): string =>
  [step.stepId, step.description, step.operationId, step.operationPath, step.channelPath, step.workflowId, step.action]
    .filter(Boolean)
    .join('\n')
    .toLowerCase()

/** The query split into lower-case terms; every term has to appear for a match. */
export const searchTerms = (query: string): string[] => query.toLowerCase().split(/\s+/).filter(Boolean)

/**
 * Filters the workflows by a free-text query; `undefined` for an empty query (show everything).
 *
 * A workflow whose id, summary or description holds every term keeps all its steps: the reader found
 * the workflow. Otherwise a step stays when every term is found in it or its workflow, at least one
 * of them in the step itself (its id, description, target or action), so `placeOrder announce` finds
 * the announce step of placeOrder and `orders send` a step that sends to an `orders…` channel.
 */
export const filterWorkflows = (views: WorkflowView[], query: string): WorkflowFilter | undefined => {
  const terms = searchTerms(query)
  if (terms.length === 0) {
    return undefined
  }

  const filter: WorkflowFilter = { workflows: new Set(), steps: new Set(), matchingSteps: 0, totalSteps: 0 }
  for (const view of views) {
    filter.totalSteps += view.steps.length
    const ownText = workflowText(view.workflow)
    const workflowMatches = terms.every((term) => ownText.includes(term))
    const steps = workflowMatches
      ? view.steps
      : view.steps.filter((entry) => {
          const text = stepText(entry.step)
          return terms.every((term) => text.includes(term) || ownText.includes(term)) && terms.some((term) => text.includes(term))
        })
    if (workflowMatches || steps.length > 0) {
      filter.workflows.add(view.id)
      steps.forEach((entry) => filter.steps.add(entry.id))
      filter.matchingSteps += steps.length
    }
  }
  return filter
}
