import type { ArazzoDocument, ArazzoStep, ArazzoWorkflow, PluginNavigationEntry } from '../types'
import { normalizeArazzoDocument } from './normalize'

/** `PluginNavigationEntry.kind` of a workflow entry. */
export const WORKFLOW_KIND = 'workflow'

/** `PluginNavigationEntry.kind` of a step entry. */
export const STEP_KIND = 'step'

/**
 * The DOM event a step's section listens for to open itself, sent before something scrolls to the
 * step (the overview diagram), so a collapsed step is not landed on folded.
 */
export const STEP_EXPAND_EVENT = 'bielu-arazzo:expand-step'

/**
 * Dispatched on a step's section to open it: it bubbles, so the workflow the step belongs to opens too (in the classic
 * layout a closed workflow hides its steps).
 */
export const expandStepEvent = (): CustomEvent => new CustomEvent(STEP_EXPAND_EVENT, { bubbles: true })

/**
 * Bubbles up from the overview diagram before it opens a step (`detail.sectionId`), so the document shows that
 * step's section, and its workflow's, even while the filter hides them.
 */
export const STEP_REVEAL_EVENT = 'bielu-arazzo:reveal-step'

/** `PluginNavigationEntry.kind` of the entry for the document's reusable components. */
export const COMPONENTS_KIND = 'components'

/** The component kinds Arazzo defines; an `x-*` extension alone does not earn a sidebar entry. */
const COMPONENT_KINDS = ['inputs', 'parameters', 'successActions', 'failureActions'] as const

/** Whether the document declares any reusable component. */
export const hasComponents = (document: ArazzoDocument): boolean =>
  COMPONENT_KINDS.some((kind) => Object.keys(document.components?.[kind] ?? {}).length > 0)

/** Whether a parsed document is an Arazzo document (`arazzo: "1.x.y"`). */
export const isArazzoDocument = (document: Record<string, unknown>): boolean =>
  typeof document.arazzo === 'string' && Array.isArray(document.workflows)

/**
 * Builds the sidebar entries: one per workflow, with one child per step, in document order. Arazzo
 * steps run in document order unless an action redirects them, so the order is never changed.
 *
 * The ids are prefixed with `documentId`, as Scalar requires, so they stay unique when several
 * Arazzo documents are loaded.
 */
export const traverseArazzoDocument = ({
  documentId,
  document,
}: {
  documentId: string
  document: Record<string, unknown>
}): PluginNavigationEntry[] => {
  const arazzo = normalizeArazzoDocument(document)
  if (!arazzo) {
    return []
  }
  const entries: PluginNavigationEntry[] = (arazzo.workflows ?? []).map((workflow) => {
    const workflowEntryId = `${documentId}/workflow/${workflow.workflowId}`
    return {
      id: workflowEntryId,
      title: workflow.summary || workflow.workflowId,
      kind: WORKFLOW_KIND,
      children: (workflow.steps ?? []).map((step) => ({
        id: `${workflowEntryId}/step/${step.stepId}`,
        title: step.stepId,
        kind: STEP_KIND,
      })),
    }
  })
  // After the workflows, as OpenAPI's "Models" come after its operations.
  if (hasComponents(arazzo)) {
    entries.push({ id: `${documentId}/components`, title: 'Components', kind: COMPONENTS_KIND })
  }
  return entries
}

/** The sidebar entry id of the components section, when the document has one. */
export const componentsEntryId = (navigation: PluginNavigationEntry[]): string | undefined =>
  navigation.find((entry) => entry.kind === COMPONENTS_KIND)?.id

/** A workflow paired with the sidebar entry Scalar scrolls to, and likewise for each of its steps. */
export type WorkflowView = {
  id: string
  workflow: ArazzoWorkflow
  steps: { id: string; step: ArazzoStep }[]
}

/**
 * Pairs the document's workflows and steps with the navigation entries Scalar hands back.
 *
 * The content component does not receive `documentId`, so it cannot rebuild the ids itself. Both
 * lists come from `traverseArazzoDocument` over the same document, so they line up by position.
 */
export const toWorkflowViews = (document: ArazzoDocument, navigation: PluginNavigationEntry[]): WorkflowView[] => {
  const workflowEntries = navigation.filter((entry) => entry.kind === WORKFLOW_KIND)
  return (document.workflows ?? []).flatMap((workflow, index) => {
    const entry = workflowEntries[index]
    if (!entry) {
      return []
    }
    const stepEntries = (entry.children ?? []).filter((child) => child.kind === STEP_KIND)
    return [
      {
        id: entry.id,
        workflow,
        steps: (workflow.steps ?? []).flatMap((step, stepIndex) => {
          const stepEntry = stepEntries[stepIndex]
          return stepEntry ? [{ id: stepEntry.id, step }] : []
        }),
      },
    ]
  })
}

/** How long a section a link landed on stays highlighted, in milliseconds. */
const FLASH_MS = 1600

/**
 * Goes to an element of the document from a link inside it: shows the step it is in even while the
 * filter hides it (`STEP_REVEAL_EVENT`), unfolds that step (and, in the classic layout, its workflow),
 * scrolls the element into view and highlights it briefly, so a reader sees which row a link meant.
 */
export const goToAnchor = async (
  from: Element,
  target: { anchor?: string; stepSection?: string },
  nextTick: () => Promise<void>,
): Promise<void> => {
  if (!target.anchor) {
    return
  }
  if (target.stepSection) {
    from.dispatchEvent(new CustomEvent(STEP_REVEAL_EVENT, { bubbles: true, detail: { sectionId: target.stepSection } }))
  }
  await nextTick()
  const owner = target.stepSection ? document.getElementById(target.stepSection) : document.getElementById(target.anchor)
  owner?.dispatchEvent(expandStepEvent())
  await nextTick()
  const element = document.getElementById(target.anchor) ?? owner
  if (!element) {
    return
  }
  element.scrollIntoView({ behavior: 'smooth', block: 'center' })
  // An outputs row is a <dt> and its <dd>: the value is part of what the link meant.
  const flashed = element.tagName === 'DT' && element.nextElementSibling?.tagName === 'DD' ? [element, element.nextElementSibling] : [element]
  flashed.forEach((target) => target.classList.add('ba-flash'))
  setTimeout(() => flashed.forEach((target) => target.classList.remove('ba-flash')), FLASH_MS)
}
