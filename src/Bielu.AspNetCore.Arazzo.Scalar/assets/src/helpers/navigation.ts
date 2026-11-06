import type { ArazzoDocument, ArazzoStep, ArazzoWorkflow, PluginNavigationEntry } from '../types'

/** `PluginNavigationEntry.kind` of a workflow entry. */
export const WORKFLOW_KIND = 'workflow'

/** `PluginNavigationEntry.kind` of a step entry. */
export const STEP_KIND = 'step'

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
}): PluginNavigationEntry[] =>
  ((document as ArazzoDocument).workflows ?? []).map((workflow) => {
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
