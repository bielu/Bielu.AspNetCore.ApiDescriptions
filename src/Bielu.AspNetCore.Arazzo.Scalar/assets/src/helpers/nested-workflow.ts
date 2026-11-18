import type { ArazzoDocument, ArazzoStep } from '../types'
import { localWorkflow } from './workflow-graph'

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
 * step that invokes a workflow is named, not expanded). Only workflows of this document have steps to show.
 */
export const nestedWorkflowSteps = (document: ArazzoDocument, step: ArazzoStep): NestedWorkflowSummary | undefined => {
  const workflow = localWorkflow(document, step.workflowId)
  return workflow?.steps?.length ? { workflowId: workflow.workflowId, steps: workflow.steps.map(summarise) } : undefined
}
