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
