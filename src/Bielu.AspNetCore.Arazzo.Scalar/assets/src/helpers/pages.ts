import type { ArazzoDocument, ArazzoWorkflow } from '../types'
import { effectiveFailureActions, effectiveSuccessActions } from './actions'

/**
 * Where the Pages view finds its data, all specification extensions (`x-…`), all optional:
 * - `pagesExtension`: on the document, a map of page id → `{ title, route, description }`;
 * - `pageExtension`: on a step, the page the user is on during it; on an action, the page they land on;
 * - `labelExtension`: on a workflow, a step or an action, a plain-language label;
 * - `endLabelExtension`: the Technical view's end-node extension, which also names an end action's page
 *   when `pageExtension` doesn't.
 */
export type PagesConfig = {
  pagesExtension?: string
  pageExtension?: string
  labelExtension?: string
  endLabelExtension?: string
}

/** A page of the app, as the document's pages map describes it. */
export type PageInfo = { title?: string; route?: string; description?: string }

const asRecord = (value: unknown): Record<string, unknown> | undefined =>
  value && typeof value === 'object' && !Array.isArray(value) ? (value as Record<string, unknown>) : undefined

const text = (value: unknown): string | undefined => (typeof value === 'string' && value.trim() ? value : undefined)

/** The document's pages, by id; entries that aren't objects are skipped. */
export const pageRegistry = (document: ArazzoDocument, config: PagesConfig): Record<string, PageInfo> => {
  const map = config.pagesExtension ? asRecord((document as Record<string, unknown>)[config.pagesExtension]) : undefined
  const pages: Record<string, PageInfo> = {}
  for (const [id, value] of Object.entries(map ?? {})) {
    const page = asRecord(value)
    if (page) {
      pages[id] = { title: text(page.title), route: text(page.route), description: text(page.description) }
    }
  }
  return pages
}

/** The plain-language label of a workflow, step or action. */
export const labelOf = (of: object | undefined, config: PagesConfig): string | undefined =>
  config.labelExtension && of ? text((of as Record<string, unknown>)[config.labelExtension]) : undefined

/** The page a step is on, or an action lands on (falling back to the end-label extension for actions). */
export const pageOf = (of: object | undefined, config: PagesConfig, isAction = false): string | undefined => {
  if (!of) {
    return undefined
  }
  const record = of as Record<string, unknown>
  const page = config.pageExtension ? record[config.pageExtension] : undefined
  if (typeof page === 'string' || typeof page === 'number') {
    return String(page)
  }
  const end = isAction && config.endLabelExtension ? record[config.endLabelExtension] : undefined
  return typeof end === 'string' || typeof end === 'number' ? String(end) : undefined
}

/** How a page is named: its title, or its id when the pages map doesn't describe it. */
export const pageTitle = (pages: Record<string, PageInfo>, pageId: string): string => pages[pageId]?.title ?? pageId

const workflowUses = (document: ArazzoDocument, workflow: ArazzoWorkflow, config: PagesConfig): boolean =>
  Boolean(labelOf(workflow, config)) ||
  (workflow.steps ?? []).some(
    (step) =>
      Boolean(labelOf(step, config) || pageOf(step, config)) ||
      // The actions a step runs, workflow-level ones included: an inherited action with a page counts.
      [...effectiveSuccessActions(document, workflow, step), ...effectiveFailureActions(document, workflow, step)].some((action) =>
        Boolean(labelOf(action, config) || (config.pageExtension && pageOf(action, { pageExtension: config.pageExtension }, true))),
      ),
  )

/**
 * Whether the Pages view has anything to show: one of its extensions is configured and the document uses it
 * (it has a pages map, or a workflow, step or action has a page or a label).
 */
export const pagesInUse = (document: ArazzoDocument, config: PagesConfig): boolean => {
  if (!config.pagesExtension && !config.pageExtension && !config.labelExtension) {
    return false
  }
  return Object.keys(pageRegistry(document, config)).length > 0 || (document.workflows ?? []).some((workflow) => workflowUses(document, workflow, config))
}
