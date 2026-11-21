import type { ArazzoDocument, ArazzoStep, ArazzoWorkflow } from '../types'
import { parseExpression, stepExpressions, workflowExpressions } from './expressions'
import type { SpecFamily } from './target-spec'
import { workflowLabel, type WorkflowGraph, type WorkflowGraphEdge, type WorkflowGraphNode } from './workflow-graph'

/** The node standing for the workflow's inputs. */
export const INPUTS_NODE_ID = '::inputs'

/** The node standing for the workflow's own outputs. */
export const OUTPUTS_NODE_ID = '::outputs'

/** The node for another workflow whose outputs a step reads. */
export const dataWorkflowNodeId = (workflowId: string): string => `::workflow:${workflowId}`

/** Where a value read by a Runtime Expression comes from, as a node of the data-flow graph, and under which name. */
const producerOf = (expression: string, workflow: ArazzoWorkflow, stepIds: Set<string>): { node: string; name: string } | undefined => {
  const parsed = parseExpression(expression)
  switch (parsed.source) {
    case 'inputs':
      return { node: INPUTS_NODE_ID, name: parsed.name }
    case 'steps':
      return stepIds.has(parsed.stepId) && parsed.field === 'outputs' && parsed.name ? { node: parsed.stepId, name: parsed.name } : undefined
    case 'workflows':
      if (parsed.field !== 'outputs' || !parsed.name) {
        return undefined
      }
      // The workflow's own outputs read back are not a flow between nodes.
      return parsed.workflowId === workflow.workflowId ? undefined : { node: dataWorkflowNodeId(parsed.workflowId), name: parsed.name }
    default:
      return undefined
  }
}

/**
 * The workflow as data passed between its parts, rather than as the order its steps run in: its inputs, each
 * step, the other workflows whose outputs it reads, and its own outputs, with an arrow from where each value
 * comes from to everything that reads it, labelled with the names it carries ("orderId, total").
 *
 * Built from the same Runtime Expressions the step details link (`$inputs.<name>`,
 * `$steps.<id>.outputs.<name>`, `$workflows.<id>.outputs.<name>`). A step that reads and produces nothing is
 * still drawn, so the graph shows every step. Workflow-level parameters, which every step receives, are left
 * out: an arrow into every step would say less than the step details do.
 */
export const buildDataFlowGraph = (
  document: ArazzoDocument,
  workflow: ArazzoWorkflow | undefined,
  specOf?: (step: ArazzoStep, document: ArazzoDocument) => SpecFamily | undefined,
): WorkflowGraph => {
  const graph: WorkflowGraph = { nodes: [], edges: [], groups: [], bands: [] }
  const steps = workflow?.steps ?? []
  if (!workflow || !steps.length) {
    return graph
  }
  const stepIds = new Set(steps.map((step) => step.stepId))

  /** producer → consumer → the names passed. */
  const flows = new Map<string, Map<string, Set<string>>>()
  const read = (consumer: string, expressions: string[]) => {
    for (const expression of expressions) {
      const producer = producerOf(expression, workflow, stepIds)
      if (!producer || producer.node === consumer) {
        continue
      }
      const byConsumer = flows.get(producer.node) ?? new Map<string, Set<string>>()
      flows.set(producer.node, byConsumer)
      byConsumer.set(consumer, (byConsumer.get(consumer) ?? new Set()).add(producer.name))
    }
  }
  for (const step of steps) {
    read(step.stepId, stepExpressions(document, step))
  }
  read(OUTPUTS_NODE_ID, workflowExpressions(document, { ...workflow, parameters: undefined, successActions: undefined, failureActions: undefined }))

  const producers = new Set(flows.keys())
  const consumers = new Set([...flows.values()].flatMap((byConsumer) => [...byConsumer.keys()]))

  if (producers.has(INPUTS_NODE_ID)) {
    graph.nodes.push({ id: INPUTS_NODE_ID, label: 'Inputs', kind: 'data' })
  }
  for (const producer of producers) {
    if (producer.startsWith('::workflow:')) {
      const workflowId = producer.slice('::workflow:'.length)
      graph.nodes.push({ id: producer, label: workflowLabel(workflowId), kind: 'workflow', workflowId })
    }
  }
  for (const step of steps) {
    const spec = specOf?.(step, document)
    const node: WorkflowGraphNode = step.workflowId
      ? { id: step.stepId, label: step.stepId, kind: 'workflow', workflowId: step.workflowId, stepId: step.stepId, ...(spec ? { spec } : {}) }
      : { id: step.stepId, label: step.stepId, kind: 'step', stepId: step.stepId, ...(spec ? { spec } : {}) }
    graph.nodes.push(node)
  }
  if (consumers.has(OUTPUTS_NODE_ID)) {
    graph.nodes.push({ id: OUTPUTS_NODE_ID, label: 'Workflow outputs', kind: 'data' })
  }

  const order = new Map(graph.nodes.map((node, index) => [node.id, index]))
  const edges: WorkflowGraphEdge[] = []
  for (const [from, byConsumer] of flows) {
    for (const [to, names] of byConsumer) {
      edges.push({ from, to, kind: 'next', label: [...names].sort().join(', ') })
    }
  }
  // In the order the nodes are drawn, so the layout reads top-down the way the document does.
  edges.sort((left, right) => (order.get(left.from)! - order.get(right.from)!) || (order.get(left.to)! - order.get(right.to)!))
  graph.edges.push(...edges)
  return graph
}
