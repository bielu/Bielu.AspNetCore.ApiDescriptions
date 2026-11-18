import type { ArazzoDocument, ArazzoStep } from '../types'
import { resolveWorkflow } from './resolve-target'

/** One step of an invoked workflow, as the step list shows it: its id and what it calls. */
export type NestedStepSummary = {
  stepId: string
  /** The operation id, operation path, channel path or workflow id the step targets. */
  target: string
  kind: 'operation' | 'channel' | 'workflow'
}

export type NestedWorkflowSummary = { workflowId: string; steps: NestedStepSummary[] }

const summarise = (step: ArazzoStep): NestedStepSummary => {
  if (step.workflowId) {
    return { stepId: step.stepId, target: step.workflowId, kind: 'workflow' }
  }
  if (step.channelPath) {
    return { stepId: step.stepId, target: step.channelPath, kind: 'channel' }
  }
  return { stepId: step.stepId, target: step.operationId ?? step.operationPath ?? '', kind: 'operation' }
}

/**
 * The steps of the workflow a step invokes, for the step list to show inline (read-only, one level: a nested
 * step that invokes a workflow is named, not expanded). The workflow is this document's, or, for
 * `$sourceDescriptions.<name>.<id>`, that of another Arazzo document in `documents`.
 */
export const nestedWorkflowSteps = (
  document: ArazzoDocument,
  step: ArazzoStep,
  documents: Record<string, unknown> = {},
): NestedWorkflowSummary | undefined => {
  const workflow = resolveWorkflow(document, step.workflowId, documents)?.workflow
  return workflow?.steps?.length ? { workflowId: workflow.workflowId, steps: workflow.steps.map(summarise) } : undefined
}
