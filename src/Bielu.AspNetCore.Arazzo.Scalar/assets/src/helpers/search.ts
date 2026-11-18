import { effectiveFailureActions, effectiveSuccessActions } from './actions'
import type { WorkflowView } from './navigation'
import { labelOf, pageOf, pageRegistry, type PageInfo, type PagesConfig } from './pages'
import type { ArazzoDocument, ArazzoStep, ArazzoWorkflow } from '../types'

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

/**
 * Where the Pages view's words come from, so a reader can search what the overview shows them: the document
 * (for its pages map and workflow-level actions) and the configured extensions.
 */
export type SearchPages = { document: ArazzoDocument; config: PagesConfig }

/** A page's id, title and route, as the Pages view shows them. */
const pageWords = (pages: Record<string, PageInfo>, pageId: string | undefined): (string | undefined)[] =>
  pageId ? [pageId, pages[pageId]?.title, pages[pageId]?.route] : []

const workflowText = (workflow: ArazzoWorkflow, pages?: SearchPages): string =>
  [workflow.workflowId, workflow.summary, workflow.description, pages && labelOf(workflow, pages.config)]
    .filter(Boolean)
    .join('\n')
    .toLowerCase()

/**
 * The fields a reader would search a step by: its id and description, and what it targets; with the Pages view
 * configured, also its label, its page's title and route, and the labels and landing pages of its actions.
 */
const stepText = (step: ArazzoStep, workflow: ArazzoWorkflow, pages?: SearchPages & { registry: Record<string, PageInfo> }): string => {
  const fields: (string | undefined)[] = [
    step.stepId,
    step.description,
    step.operationId,
    step.operationPath,
    step.channelPath,
    step.workflowId,
    step.action,
  ]
  if (pages) {
    const { registry } = pages
    fields.push(labelOf(step, pages.config), ...pageWords(registry, pageOf(step, pages.config)))
    const actions = [...effectiveSuccessActions(pages.document, workflow, step), ...effectiveFailureActions(pages.document, workflow, step)]
    for (const action of actions) {
      fields.push(labelOf(action, pages.config), ...pageWords(registry, pageOf(action, pages.config, true)))
    }
  }
  return fields.filter(Boolean).join('\n').toLowerCase()
}

/** The query split into lower-case terms; every term has to appear for a match. */
export const searchTerms = (query: string): string[] => query.toLowerCase().split(/\s+/).filter(Boolean)

/**
 * Filters the workflows by a free-text query; `undefined` for an empty query (show everything).
 *
 * A workflow whose id, summary or description holds every term keeps all its steps: the reader found
 * the workflow. Otherwise a step stays when every term is found in it or its workflow, at least one
 * of them in the step itself (its id, description, target or action), so `placeOrder announce` finds
 * the announce step of placeOrder and `orders send` a step that sends to an `orders…` channel. With
 * `pages`, the labels, page titles and routes the Pages view shows count too.
 */
export const filterWorkflows = (views: WorkflowView[], query: string, pages?: SearchPages): WorkflowFilter | undefined => {
  const terms = searchTerms(query)
  if (terms.length === 0) {
    return undefined
  }

  // The Pages view's words count only when one of its options is set; the page map is read once per filter pass.
  const { pagesExtension, pageExtension, labelExtension } = pages?.config ?? {}
  const withPages = pages && (pagesExtension || pageExtension || labelExtension) ? { ...pages, registry: pageRegistry(pages.document, pages.config) } : undefined
  const filter: WorkflowFilter = { workflows: new Set(), steps: new Set(), matchingSteps: 0, totalSteps: 0 }
  for (const view of views) {
    filter.totalSteps += view.steps.length
    const ownText = workflowText(view.workflow, withPages)
    const workflowMatches = terms.every((term) => ownText.includes(term))
    const steps = workflowMatches
      ? view.steps
      : view.steps.filter((entry) => {
          const text = stepText(entry.step, view.workflow, withPages)
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
