import dagre from '@dagrejs/dagre'
import type { ArazzoCriterion, ArazzoDocument, ArazzoFailureAction, ArazzoStep, ArazzoSuccessAction, ArazzoWorkflow } from '../types'
import { resolveReusables } from './reusable'
import type { SpecFamily } from './target-spec'

/**
 * A box in the workflow overview: a step, a workflow a step or an action invokes, or the end node an
 * `end` action leads to.
 */
export type WorkflowGraphNode = {
  id: string
  label: string
  kind: 'step' | 'workflow' | 'success' | 'failure'
  /** For a step: the spec its target is described in, drawn as a colored marker. */
  spec?: SpecFamily
  /** For a workflow node: the workflow it invokes (`$sourceDescriptions.<name>.<id>` when in another document). */
  workflowId?: string
  /** For a workflow node: whether it invokes a workflow of this document, so its steps can be shown inside the diagram. */
  expandable?: boolean
  /** For a workflow node: whether its steps are shown, in the group `${id}::group`. */
  expanded?: boolean
  /** For a workflow node: the workflow is already being expanded further up, so it is not expanded again. */
  cycle?: boolean
  /** The group (an expanded workflow) the node is drawn in; top-level nodes have none. */
  group?: string
  /** For a step: its `stepId`, so a top-level step box can open that step's section. */
  stepId?: string
  /** For an end node: the actions that lead to it, for its tooltip. */
  actions?: string[]
}

/** An arrow between two nodes. `kind` says why it is drawn, so success and failure paths can be told apart. */
export type WorkflowGraphEdge = {
  from: string
  to: string
  label?: string
  /**
   * `next`/`success`/`failure` as before; `return` goes back from a workflow a `retry` action invoked to
   * the step that retries; `expand` goes from an expanded workflow node into its first steps.
   */
  kind: 'next' | 'success' | 'failure' | 'return' | 'expand'
}

/** An expanded workflow: a box drawn around the steps of the workflow a workflow node invokes. */
export type WorkflowGraphGroup = {
  id: string
  label: string
  workflowId: string
  /** The group this one is nested in, when it was expanded from inside another expanded workflow. */
  parent?: string
}

export type WorkflowGraph = {
  nodes: WorkflowGraphNode[]
  edges: WorkflowGraphEdge[]
  groups: WorkflowGraphGroup[]
}

export type WorkflowGraphOptions = {
  /** The ids of the workflow nodes whose steps are shown (see {@link WorkflowGraphNode.expandable}). */
  expanded?: ReadonlySet<string>
  /**
   * A specification extension (such as `x-page`) whose value names the end node of the `end`
   * failure actions that carry it, in place of the action's name.
   */
  endLabelExtension?: string
}

export const SUCCESS_NODE_ID = '__workflow_success__'
export const FAILURE_NODE_ID = '__workflow_failure__'

/** The id of the node for the workflow `workflowId` that actions in the scope `prefix` invoke. */
export const workflowNodeId = (prefix: string, workflowId: string): string => `${prefix}__workflow__:${workflowId}`

/** The id of a distinct end node (one per end action name, or per value of the end-label extension). */
export const endNodeId = (prefix: string, key: string): string => `${prefix}__end__:${key}`

/** The id of the group an expanded workflow node's steps are drawn in. */
export const groupId = (nodeId: string): string => `${nodeId}::group`

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

/** A workflow of this document, by a plain id; `$sourceDescriptions.<name>.<id>` names one in another document. */
export const localWorkflow = (document: ArazzoDocument, workflowId: string | undefined): ArazzoWorkflow | undefined =>
  workflowId && !workflowId.startsWith('$') ? document.workflows?.find((workflow) => workflow.workflowId === workflowId) : undefined

/** What an `end` failure action's end node is called: the extension's value when configured and present, else its name. */
const endLabelOf = (action: ArazzoFailureAction, extension: string | undefined): { key: string; label: string; named: boolean } => {
  const value = extension ? (action as Record<string, unknown>)[extension] : undefined
  if (typeof value === 'string' || typeof value === 'number') {
    return { key: `${extension}=${value}`, label: String(value), named: true }
  }
  return { key: action.name ?? '', label: action.name || 'Failed', named: false }
}

type Scope = {
  /** Prepended to every node id of the scope, so the same step can appear at several levels. */
  prefix: string
  /** The group the scope's nodes are drawn in. */
  group?: string
  /** The workflows on the expansion path, the drawn one first: expanding one of them again would never end. */
  path: string[]
}

/**
 * Builds the overview of a workflow's steps, the same graph the in-core Scalar POC drew with Mermaid.
 *
 * Two kinds of arrows, never drawn twice for the same pair of steps:
 * - `onSuccess`/`onFailure` actions give labeled arrows to their `goto`/`retry` step or workflow, or to an
 *   end node for `end`. They are the only conditional arrows: `dependsOn` records prerequisites, not which
 *   branch is taken.
 * - Everything else follows `dependsOn`, labeled with the source step's `successCriteria` when that
 *   step branches, so every arrow out of a branch point reads the same way. When no step declares
 *   `dependsOn`, the steps are chained in document order, the order Arazzo runs them in by default.
 *
 * Workflows are nodes of their own: a step that invokes one, and one node per workflow that `goto`/`retry`
 * actions invoke (shared by every step that does). A `retry` through a workflow also gets an arrow back to
 * the step, since the step runs again once the workflow is done. A workflow node of this document can be
 * expanded (`options.expanded`): its steps are then drawn in a group, recursively, except a workflow that is
 * already on the expansion path, which stays collapsed and is marked as a cycle.
 *
 * `end` success actions lead to one Success node. `end` failure actions lead to one Failed node while they
 * cannot be told apart (one name, no end-label extension); otherwise each gets its own end node, named after
 * the action or the value of `options.endLabelExtension`.
 *
 * A retry with neither `stepId` nor `workflowId` retries the step itself; a self-loop on the box adds
 * nothing its label would not say, so none is drawn.
 */
export const buildWorkflowGraph = (
  document: ArazzoDocument,
  workflow: ArazzoWorkflow | undefined,
  specOf?: (step: ArazzoStep) => SpecFamily | undefined,
  options: WorkflowGraphOptions = {},
): WorkflowGraph => {
  const graph: WorkflowGraph = { nodes: [], edges: [], groups: [] }
  if (!workflow?.steps?.length) {
    return graph
  }
  addScope(graph, document, workflow, { prefix: '', path: [workflow.workflowId] }, specOf, options)
  return graph
}

/** Adds one workflow's steps, arrows and end nodes to the graph, then the scopes of its expanded workflow nodes. */
const addScope = (
  graph: WorkflowGraph,
  document: ArazzoDocument,
  workflow: ArazzoWorkflow,
  scope: Scope,
  specOf: ((step: ArazzoStep) => SpecFamily | undefined) | undefined,
  options: WorkflowGraphOptions,
): string[] => {
  const steps = workflow.steps ?? []
  const { prefix, group } = scope
  const at = (id: string) => `${prefix}${id}`
  const inGroup = <T extends WorkflowGraphNode>(node: T): T => (group ? { ...node, group } : node)
  const workflowNodes: WorkflowGraphNode[] = []

  /** A node for a workflow this scope invokes; expandable when it is one of this document's. */
  const workflowNode = (id: string, label: string, workflowId: string, extra: Partial<WorkflowGraphNode> = {}): WorkflowGraphNode => {
    const local = localWorkflow(document, workflowId)
    const cycle = Boolean(local && scope.path.includes(workflowId))
    const expandable = Boolean(local && !cycle && local.steps?.length)
    const node: WorkflowGraphNode = inGroup({
      id,
      label,
      kind: 'workflow',
      workflowId,
      expandable,
      expanded: expandable && Boolean(options.expanded?.has(id)),
      ...(cycle ? { cycle } : {}),
      ...extra,
    })
    workflowNodes.push(node)
    return node
  }

  for (const step of steps) {
    const spec = specOf?.(step)
    if (step.workflowId) {
      graph.nodes.push(workflowNode(at(step.stepId), step.stepId, step.workflowId, { stepId: step.stepId, ...(spec ? { spec } : {}) }))
      continue
    }
    const node: WorkflowGraphNode = inGroup({ id: at(step.stepId), label: step.stepId, kind: 'step', stepId: step.stepId })
    graph.nodes.push(spec ? { ...node, spec } : node)
  }

  const stepIds = new Set(steps.map((step) => step.stepId))
  const explicit = new Set<string>()
  const actionEdges: WorkflowGraphEdge[] = []
  const invoked = new Map<string, WorkflowGraphNode>()
  const invokedNode = (workflowId: string): WorkflowGraphNode => {
    let node = invoked.get(workflowId)
    if (!node) {
      node = workflowNode(workflowNodeId(prefix, workflowId), workflowId, workflowId, specOf ? { spec: 'arazzo' } : {})
      invoked.set(workflowId, node)
    }
    return node
  }
  let usesSuccess = false
  const failureEnds: { from: string; action: ArazzoFailureAction }[] = []

  for (const step of steps) {
    const from = at(step.stepId)
    for (const action of resolveReusables<ArazzoSuccessAction>(document, step.onSuccess)) {
      if (action.type === 'end') {
        usesSuccess = true
        actionEdges.push({ from, to: at(SUCCESS_NODE_ID), label: actionLabel(action), kind: 'success' })
      } else if (action.stepId && stepIds.has(action.stepId)) {
        explicit.add(`${step.stepId}->${action.stepId}`)
        actionEdges.push({ from, to: at(action.stepId), label: describeCriteria(action.criteria), kind: 'success' })
      } else if (action.workflowId) {
        actionEdges.push({ from, to: invokedNode(action.workflowId).id, label: actionLabel(action), kind: 'success' })
      }
    }
    for (const action of resolveReusables<ArazzoFailureAction>(document, step.onFailure)) {
      if (action.type === 'end') {
        failureEnds.push({ from, action })
      } else if (action.stepId && stepIds.has(action.stepId)) {
        explicit.add(`${step.stepId}->${action.stepId}`)
        actionEdges.push({ from, to: at(action.stepId), label: actionLabel(action, retryExtra(action)), kind: 'failure' })
      } else if (action.workflowId) {
        const target = invokedNode(action.workflowId).id
        actionEdges.push({ from, to: target, label: actionLabel(action, retryExtra(action)), kind: 'failure' })
        if (action.type === 'retry') {
          actionEdges.push({ from: target, to: from, kind: 'return' })
        }
      }
    }
  }

  // End failure actions: one Failed node while they can't be told apart, else one node each.
  const ends = failureEnds.map((entry) => ({ ...entry, end: endLabelOf(entry.action, options.endLabelExtension) }))
  const distinct = new Set(ends.map((entry) => entry.end.key))
  const separate = ends.some((entry) => entry.end.named) || distinct.size > 1
  const endNodes = new Map<string, WorkflowGraphNode>()
  for (const { from, action, end } of ends) {
    const id = separate ? endNodeId(prefix, end.key) : at(FAILURE_NODE_ID)
    let node = endNodes.get(id)
    if (!node) {
      node = inGroup({ id, label: separate ? end.label : 'Failed', kind: 'failure', ...(separate ? { actions: [] } : {}) })
      endNodes.set(id, node)
    }
    if (node.actions && action.name && !node.actions.includes(action.name)) {
      node.actions.push(action.name)
    }
    actionEdges.push({ from, to: id, label: actionLabel(action), kind: 'failure' })
  }

  const nextEdges: WorkflowGraphEdge[] = []
  const usesDependsOn = steps.some((step) => step.dependsOn?.length)
  if (usesDependsOn) {
    for (const step of steps) {
      for (const dependency of step.dependsOn ?? []) {
        const source = steps.find((candidate) => candidate.stepId === dependency)
        if (!source || explicit.has(`${dependency}->${step.stepId}`)) {
          continue
        }
        const label = hasActions(source) ? describeCriteria(source.successCriteria) : undefined
        nextEdges.push({ from: at(dependency), to: at(step.stepId), label, kind: 'next' })
      }
    }
  } else {
    for (let index = 1; index < steps.length; index++) {
      nextEdges.push({ from: at(steps[index - 1]!.stepId), to: at(steps[index]!.stepId), kind: 'next' })
    }
  }

  // Arazzo ends a workflow successfully after its last step: draw that for every step that doesn't decide
  // where it goes on success itself and that nothing runs after (the last step, or one no step depends on).
  const dependedOn = new Set(steps.flatMap((step) => step.dependsOn ?? []))
  const lastSteps = usesDependsOn ? steps.filter((step) => !dependedOn.has(step.stepId)) : steps.slice(-1)
  const implicitEdges: WorkflowGraphEdge[] = []
  for (const step of lastSteps) {
    if (resolveReusables<ArazzoSuccessAction>(document, step.onSuccess).length) {
      continue
    }
    usesSuccess = true
    const criteria = describeCriteria(step.successCriteria)
    const edge: WorkflowGraphEdge = { from: at(step.stepId), to: at(SUCCESS_NODE_ID), kind: 'success' }
    implicitEdges.push(criteria && criteria.length <= MAX_EDGE_LABEL_LENGTH ? { ...edge, label: criteria } : edge)
  }

  graph.nodes.push(...[...invoked.values()])
  if (usesSuccess) {
    graph.nodes.push(inGroup({ id: at(SUCCESS_NODE_ID), label: 'Success', kind: 'success' }))
  }
  graph.nodes.push(...endNodes.values())
  graph.edges.push(...nextEdges, ...actionEdges, ...implicitEdges)

  for (const node of workflowNodes) {
    const nested = node.expanded ? localWorkflow(document, node.workflowId) : undefined
    if (!nested) {
      continue
    }
    const nestedGroup: WorkflowGraphGroup = { id: groupId(node.id), label: node.workflowId!, workflowId: node.workflowId!, ...(group ? { parent: group } : {}) }
    graph.groups.push(nestedGroup)
    const entries = addScope(
      graph,
      document,
      nested,
      { prefix: `${node.id}/`, group: nestedGroup.id, path: [...scope.path, nested.workflowId] },
      specOf,
      options,
    )
    graph.edges.push(...entries.map((entry) => ({ from: node.id, to: entry, kind: 'expand' as const })))
  }

  // The steps a run of this workflow starts at: those without prerequisites, or the first one.
  const entrySteps = usesDependsOn ? steps.filter((step) => !step.dependsOn?.length) : steps.slice(0, 1)
  return entrySteps.map((step) => at(step.stepId))
}

export type Point = { x: number; y: number }

export type LaidOutNode = WorkflowGraphNode & { x: number; y: number; width: number; height: number }

export type LaidOutEdge = WorkflowGraphEdge & { points: Point[]; labelPosition?: Point; labelWidth: number }

export type LaidOutGroup = WorkflowGraphGroup & { x: number; y: number; width: number; height: number }

export type WorkflowLayout = {
  width: number
  height: number
  nodes: LaidOutNode[]
  edges: LaidOutEdge[]
  /** Expanded workflows, outermost first, so nested groups draw on top of their parents. */
  groups: LaidOutGroup[]
}

/** Matches the diagram's font sizes in `ArazzoWorkflowDiagram.vue`; estimated so layout needs no DOM. */
const NODE_CHAR_WIDTH = 7.4
const LABEL_CHAR_WIDTH = 6.2
const NODE_HEIGHT = 36
/** The colored strip on the left of a step box that says which spec it targets. */
export const SPEC_MARKER_WIDTH = 6
const LABEL_HEIGHT = 18
/** Room for the +/− toggle on the right of an expandable workflow node. */
export const WORKFLOW_TOGGLE_WIDTH = 22
/** Room above a group's steps for its label. */
export const GROUP_LABEL_HEIGHT = 22
/** Long conditions are cut, so a single arrow cannot stretch the whole diagram sideways. */
export const MAX_EDGE_LABEL_LENGTH = 48

export const truncateLabel = (label: string): string =>
  label.length > MAX_EDGE_LABEL_LENGTH ? `${label.slice(0, MAX_EDGE_LABEL_LENGTH - 1)}…` : label

/** Lays the graph out top-down with dagre, the layout engine Mermaid's flowcharts use. */
export const layoutWorkflowGraph = (graph: WorkflowGraph): WorkflowLayout => {
  const groups = graph.groups ?? []
  const layout = new dagre.graphlib.Graph({ multigraph: true, compound: groups.length > 0 })
  layout.setGraph({ rankdir: 'TB', nodesep: 32, ranksep: 44, edgesep: 16, marginx: 8, marginy: 8 })
  layout.setDefaultEdgeLabel(() => ({}))

  for (const group of groups) {
    layout.setNode(group.id, { paddingTop: GROUP_LABEL_HEIGHT, paddingLeft: 12, paddingRight: 12, paddingBottom: 12 })
  }
  for (const group of groups) {
    if (group.parent) {
      layout.setParent(group.id, group.parent)
    }
  }
  for (const node of graph.nodes) {
    const width =
      node.kind === 'step'
        ? Math.max(96, node.label.length * NODE_CHAR_WIDTH + 28) + (node.spec ? SPEC_MARKER_WIDTH : 0)
        : node.kind === 'workflow'
          ? Math.max(96, node.label.length * NODE_CHAR_WIDTH + 28) +
            (node.spec ? SPEC_MARKER_WIDTH : 0) +
            (node.expandable || node.cycle ? WORKFLOW_TOGGLE_WIDTH : 0)
          : Math.max(84, node.label.length * NODE_CHAR_WIDTH + 28)
    layout.setNode(node.id, { width, height: NODE_HEIGHT })
    if (node.group) {
      layout.setParent(node.id, node.group)
    }
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
    groups: groups.map((group) => {
      const placed = layout.node(group.id)
      return { ...group, x: placed.x, y: placed.y, width: placed.width, height: placed.height }
    }),
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

