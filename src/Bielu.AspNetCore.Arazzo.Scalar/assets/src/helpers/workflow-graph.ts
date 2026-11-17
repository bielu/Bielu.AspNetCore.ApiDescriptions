import dagre from '@dagrejs/dagre'
import type { ArazzoCriterion, ArazzoDocument, ArazzoFailureAction, ArazzoStep, ArazzoSuccessAction, ArazzoWorkflow } from '../types'
import { resolveReusables } from './reusable'
import type { SpecFamily } from './target-spec'

/** A box in the workflow overview: a step, or the synthetic end node an `end` action leads to. */
export type WorkflowGraphNode = {
  id: string
  label: string
  kind: 'step' | 'success' | 'failure'
  /** For a step: the spec its target is described in, drawn as a colored marker. */
  spec?: SpecFamily
}

/** An arrow between two nodes. `kind` says why it is drawn, so success and failure paths can be told apart. */
export type WorkflowGraphEdge = {
  from: string
  to: string
  label?: string
  kind: 'next' | 'success' | 'failure'
}

export type WorkflowGraph = {
  nodes: WorkflowGraphNode[]
  edges: WorkflowGraphEdge[]
}

export const SUCCESS_NODE_ID = '__workflow_success__'
export const FAILURE_NODE_ID = '__workflow_failure__'

/** Joins a criteria list (all of which must hold) into one label; `undefined` when there is nothing to show. */
const describeCriteria = (criteria: ArazzoCriterion[] | undefined): string | undefined =>
  criteria?.length ? criteria.map((criterion) => criterion.condition).join(' && ') : undefined

/** The condition is what a reader wants on an arrow; the action's own name is the fallback. */
const actionLabel = (action: ArazzoSuccessAction | ArazzoFailureAction, extra?: string): string =>
  [describeCriteria(action.criteria) ?? action.name, extra].filter(Boolean).join(' — ')

const retryExtra = (action: ArazzoFailureAction): string | undefined => {
  if (action.type !== 'retry') {
    return undefined
  }
  return typeof action.retryLimit === 'number' ? `retry, ≤ ${action.retryLimit}×` : 'retry'
}

const hasActions = (step: ArazzoStep): boolean => Boolean(step.onSuccess?.length || step.onFailure?.length)

/**
 * Builds the overview of a workflow's steps, the same graph the in-core Scalar POC drew with Mermaid.
 *
 * Two kinds of arrows, never drawn twice for the same pair of steps:
 * - `onSuccess`/`onFailure` actions give labeled arrows to their `goto`/`retry` step, or to a
 *   Success/Failed end node for `end`. They are the only conditional arrows: `dependsOn` records
 *   prerequisites, not which branch is taken.
 * - Everything else follows `dependsOn`, labeled with the source step's `successCriteria` when that
 *   step branches, so every arrow out of a branch point reads the same way. When no step declares
 *   `dependsOn`, the steps are chained in document order, the order Arazzo runs them in by default.
 *
 * A retry with neither `stepId` nor `workflowId` retries the step itself; a self-loop on the box adds
 * nothing its label would not say, so none is drawn.
 */
export const buildWorkflowGraph = (
  document: ArazzoDocument,
  workflow: ArazzoWorkflow | undefined,
  specOf?: (step: ArazzoStep) => SpecFamily | undefined,
): WorkflowGraph => {
  const steps = workflow?.steps ?? []
  if (steps.length === 0) {
    return { nodes: [], edges: [] }
  }

  const nodes: WorkflowGraphNode[] = steps.map((step) => {
    const spec = specOf?.(step)
    const node: WorkflowGraphNode = { id: step.stepId, label: step.stepId, kind: 'step' }
    return spec ? { ...node, spec } : node
  })
  const stepIds = new Set(steps.map((step) => step.stepId))
  const explicit = new Set<string>()
  const actionEdges: WorkflowGraphEdge[] = []
  let usesSuccess = false
  let usesFailure = false

  for (const step of steps) {
    for (const action of resolveReusables<ArazzoSuccessAction>(document, step.onSuccess)) {
      if (action.type === 'end') {
        usesSuccess = true
        actionEdges.push({ from: step.stepId, to: SUCCESS_NODE_ID, label: actionLabel(action), kind: 'success' })
      } else if (action.stepId && stepIds.has(action.stepId)) {
        explicit.add(`${step.stepId}->${action.stepId}`)
        actionEdges.push({ from: step.stepId, to: action.stepId, label: describeCriteria(action.criteria), kind: 'success' })
      }
    }
    for (const action of resolveReusables<ArazzoFailureAction>(document, step.onFailure)) {
      if (action.type === 'end') {
        usesFailure = true
        actionEdges.push({ from: step.stepId, to: FAILURE_NODE_ID, label: actionLabel(action), kind: 'failure' })
      } else if (action.stepId && stepIds.has(action.stepId)) {
        explicit.add(`${step.stepId}->${action.stepId}`)
        actionEdges.push({ from: step.stepId, to: action.stepId, label: actionLabel(action, retryExtra(action)), kind: 'failure' })
      }
    }
  }

  const nextEdges: WorkflowGraphEdge[] = []
  if (steps.some((step) => step.dependsOn?.length)) {
    for (const step of steps) {
      for (const dependency of step.dependsOn ?? []) {
        const source = steps.find((candidate) => candidate.stepId === dependency)
        if (!source || explicit.has(`${dependency}->${step.stepId}`)) {
          continue
        }
        const label = hasActions(source) ? describeCriteria(source.successCriteria) : undefined
        nextEdges.push({ from: dependency, to: step.stepId, label, kind: 'next' })
      }
    }
  } else {
    for (let index = 1; index < steps.length; index++) {
      nextEdges.push({ from: steps[index - 1]!.stepId, to: steps[index]!.stepId, kind: 'next' })
    }
  }

  if (usesSuccess) {
    nodes.push({ id: SUCCESS_NODE_ID, label: 'Success', kind: 'success' })
  }
  if (usesFailure) {
    nodes.push({ id: FAILURE_NODE_ID, label: 'Failed', kind: 'failure' })
  }

  return { nodes, edges: [...nextEdges, ...actionEdges] }
}

export type Point = { x: number; y: number }

export type LaidOutNode = WorkflowGraphNode & { x: number; y: number; width: number; height: number }

export type LaidOutEdge = WorkflowGraphEdge & { points: Point[]; labelPosition?: Point; labelWidth: number }

export type WorkflowLayout = {
  width: number
  height: number
  nodes: LaidOutNode[]
  edges: LaidOutEdge[]
}

/** Matches the diagram's font sizes in `ArazzoWorkflowDiagram.vue`; estimated so layout needs no DOM. */
const NODE_CHAR_WIDTH = 7.4
const LABEL_CHAR_WIDTH = 6.2
const NODE_HEIGHT = 36
/** The colored strip on the left of a step box that says which spec it targets. */
export const SPEC_MARKER_WIDTH = 6
const LABEL_HEIGHT = 18
/** Long conditions are cut, so a single arrow cannot stretch the whole diagram sideways. */
export const MAX_EDGE_LABEL_LENGTH = 48

export const truncateLabel = (label: string): string =>
  label.length > MAX_EDGE_LABEL_LENGTH ? `${label.slice(0, MAX_EDGE_LABEL_LENGTH - 1)}…` : label

/** Lays the graph out top-down with dagre, the layout engine Mermaid's flowcharts use. */
export const layoutWorkflowGraph = (graph: WorkflowGraph): WorkflowLayout => {
  const layout = new dagre.graphlib.Graph({ multigraph: true })
  layout.setGraph({ rankdir: 'TB', nodesep: 32, ranksep: 44, edgesep: 16, marginx: 8, marginy: 8 })
  layout.setDefaultEdgeLabel(() => ({}))

  for (const node of graph.nodes) {
    const width =
      node.kind === 'step'
        ? Math.max(96, node.label.length * NODE_CHAR_WIDTH + 28) + (node.spec ? SPEC_MARKER_WIDTH : 0)
        : 84
    layout.setNode(node.id, { width, height: NODE_HEIGHT })
  }
  graph.edges.forEach((edge, index) => {
    const label = edge.label ? truncateLabel(edge.label) : ''
    layout.setEdge(
      edge.from,
      edge.to,
      label ? { width: label.length * LABEL_CHAR_WIDTH + 12, height: LABEL_HEIGHT, labelpos: 'c' } : {},
      String(index),
    )
  })

  dagre.layout(layout)

  const info = layout.graph()
  return {
    width: info.width ?? 0,
    height: info.height ?? 0,
    nodes: graph.nodes.map((node) => {
      const placed = layout.node(node.id)
      return { ...node, x: placed.x, y: placed.y, width: placed.width, height: placed.height }
    }),
    edges: graph.edges.map((edge, index) => {
      const placed = layout.edge({ v: edge.from, w: edge.to, name: String(index) }) as
        | { points?: Point[]; x?: number; y?: number; width?: number }
        | undefined
      return {
        ...edge,
        label: edge.label ? truncateLabel(edge.label) : undefined,
        points: placed?.points ?? [],
        labelPosition:
          edge.label && typeof placed?.x === 'number' && typeof placed?.y === 'number' ? { x: placed.x, y: placed.y } : undefined,
        labelWidth: placed?.width ?? 0,
      }
    }),
  }
}
