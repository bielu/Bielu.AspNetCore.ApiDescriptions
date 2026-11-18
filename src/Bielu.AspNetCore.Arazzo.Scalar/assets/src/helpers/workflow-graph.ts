import dagre from '@dagrejs/dagre'
import type { ArazzoCriterion, ArazzoDocument, ArazzoFailureAction, ArazzoStep, ArazzoSuccessAction, ArazzoWorkflow } from '../types'
import { effectiveFailureActions, effectiveSuccessActions, isUnconditional, reachableActions } from './actions'
import { labelOf, pageOf, pageRegistry, pageTitle, type PagesConfig } from './pages'
import { QUALIFIED_WORKFLOW, resolveWorkflow } from './resolve-target'
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
  /** For a step: the `retry` failure actions that run the step itself again (no `stepId` or `workflowId`). */
  retries?: WorkflowGraphRetry[]
  /** Pages view: the step id or workflow id behind a plain-language label, for the tooltip. */
  technical?: string
  /** Pages view: the page a step is on, or an end node shows. */
  page?: string
  /** Pages view: a second line under the label (an end node's action label). */
  subtitle?: string
}

/** A `retry` of the step itself, drawn as a badge on the step's box rather than as an arrow. */
export type WorkflowGraphRetry = {
  /** The action's criteria, or "otherwise", with its limit and delay: the same text a retry arrow carries. */
  label: string
  action?: string
  retryLimit?: number
  retryAfter?: number
}

/** An arrow between two nodes. `kind` says why it is drawn, so success and failure paths can be told apart. */
export type WorkflowGraphEdge = {
  from: string
  to: string
  label?: string
  /**
   * `next`/`success`/`failure` as before; `return` goes back from the step or workflow a `retry` action
   * runs to the step that retries; `expand` goes from an expanded workflow node into its first steps.
   */
  kind: 'next' | 'success' | 'failure' | 'return' | 'expand'
  /** For an `onSuccess`/`onFailure` arrow: the action's name, which the label leaves out when it shows criteria. */
  action?: string
  /**
   * Pages view: the page an arrow into an end node lands on, when that end node is shared with arrows landing
   * elsewhere (its label then ends with "→ <page title>"). When every arrow into an end node lands on the same
   * page, the end node carries the page instead.
   */
  page?: string
}

/** An expanded workflow: a box drawn around the steps of the workflow a workflow node invokes. */
export type WorkflowGraphGroup = {
  id: string
  label: string
  workflowId: string
  /** The group this one is nested in, when it was expanded from inside another expanded workflow. */
  parent?: string
  /**
   * The workspace name of the Arazzo document the group's workflow is in, when that is another document than the
   * one drawn: its steps have no section on this page.
   */
  documentName?: string
}

/** Pages view: consecutive steps on the same page (or on none), drawn as one labelled band. */
export type WorkflowGraphBand = {
  id: string
  /** The page id; none for the steps without a page. */
  pageId?: string
  title: string
  route?: string
  nodes: string[]
  /** The group (an expanded workflow) the band's steps are in. */
  group?: string
}

export type WorkflowGraph = {
  nodes: WorkflowGraphNode[]
  edges: WorkflowGraphEdge[]
  groups: WorkflowGraphGroup[]
  /** Pages view only; empty in the Technical view. */
  bands: WorkflowGraphBand[]
}

/** The two ways the overview can be read: the technical flow, or the pages a user goes through. */
export type WorkflowView = 'technical' | 'pages'

/** What a band of steps without a page is called. */
export const NO_PAGE_TITLE = 'No page'

/** Pages view: what the arrow to Success after the last step says. */
export const DONE_LABEL = 'done'

export type WorkflowGraphOptions = {
  /** The ids of the workflow nodes whose steps are shown (see {@link WorkflowGraphNode.expandable}). */
  expanded?: ReadonlySet<string>
  /**
   * A specification extension (such as `x-page`) whose value names the end node of the `end`
   * failure actions that carry it, in place of the action's name.
   */
  endLabelExtension?: string
  /** Which view to build: `technical` (the default) or `pages`, which uses {@link WorkflowGraphOptions.pages}. */
  view?: WorkflowView
  /** Where the Pages view finds pages and labels. */
  pages?: PagesConfig
  /**
   * The documents Scalar has loaded, by workspace name, so a `$sourceDescriptions.<name>.<workflowId>` workflow
   * of another loaded Arazzo document can be expanded too.
   */
  documents?: Record<string, unknown>
  /**
   * The workspace name of the document drawn, so a workflow of another document that calls back into it is
   * recognised as already on the expansion path.
   */
  documentName?: string
}

/** How a workflow id reads on a box: `identity.signIn` for `$sourceDescriptions.identity.signIn`, else as written. */
export const workflowLabel = (workflowId: string): string => {
  const qualified = QUALIFIED_WORKFLOW.exec(workflowId)
  return qualified ? `${qualified[1]}.${qualified[2]}` : workflowId
}

/**
 * The ids of the synthetic nodes contain `:`, which a `stepId` cannot (Arazzo: `[A-Za-z0-9_\-]+`), so a
 * step can never share an id with an end node, a workflow node or another synthetic box.
 */
export const SUCCESS_NODE_ID = '::success'
export const FAILURE_NODE_ID = '::failure'

/** The id of the node for the workflow `workflowId` that actions in the scope `prefix` invoke. */
export const workflowNodeId = (prefix: string, workflowId: string): string => `${prefix}__workflow__:${workflowId}`

/** The id of a distinct end node (one per end action name, or per value of the end-label extension). */
export const endNodeId = (prefix: string, key: string): string => `${prefix}__end__:${key}`

/** The id of the group an expanded workflow node's steps are drawn in. */
export const groupId = (nodeId: string): string => `${nodeId}::group`

/** Joins a criteria list (all of which must hold) into one label; `undefined` when there is nothing to show. */
const describeCriteria = (criteria: ArazzoCriterion[] | undefined): string | undefined =>
  criteria?.length ? criteria.map((criterion) => criterion.condition).join(' && ') : undefined

/** Spreads a `page` field only when there is a page. */
const pageField = (page: string | undefined): { page?: string } => (page ? { page } : {})

/** What an action's arrow is labeled with when the action has no criteria: it is the catch-all branch. */
export const OTHERWISE_LABEL = 'otherwise'

/**
 * The condition is what a reader wants on an arrow; an action without criteria is the catch-all, "otherwise".
 * The action's name goes on the edge separately (`action`), for the tooltip.
 */
const actionLabel = (action: ArazzoSuccessAction | ArazzoFailureAction, extra?: string, label?: string): string =>
  [label ?? describeCriteria(action.criteria) ?? OTHERWISE_LABEL, extra].filter(Boolean).join(' — ')

/** An arrow drawn for an action, carrying the action's name. */
const actionEdge = (
  from: string,
  to: string,
  kind: 'success' | 'failure',
  action: ArazzoSuccessAction | ArazzoFailureAction,
  extra?: string,
  label?: string,
): WorkflowGraphEdge => {
  const edge: WorkflowGraphEdge = { from, to, kind, label: actionLabel(action, extra, label) }
  return action.name ? { ...edge, action: action.name } : edge
}

/** "retry, ≤ 3×, after 2s": a retry's limit and delay, worded as the step details word them. */
const retryExtra = (action: ArazzoFailureAction): string | undefined => {
  if (action.type !== 'retry') {
    return undefined
  }
  return [
    'retry',
    typeof action.retryLimit === 'number' ? `≤ ${action.retryLimit}×` : '',
    typeof action.retryAfter === 'number' ? `after ${action.retryAfter}s` : '',
  ]
    .filter(Boolean)
    .join(', ')
}

const selfRetry = (action: ArazzoFailureAction): WorkflowGraphRetry => ({
  label: actionLabel(action, retryExtra(action)),
  ...(action.name ? { action: action.name } : {}),
  ...(typeof action.retryLimit === 'number' ? { retryLimit: action.retryLimit } : {}),
  ...(typeof action.retryAfter === 'number' ? { retryAfter: action.retryAfter } : {}),
})

/** What an `end` failure action's end node is called: the extension's value when configured and present, else its name. */
const endLabelOf = (action: ArazzoFailureAction, extension: string | undefined): { key: string; label: string; named: boolean } => {
  const value = extension ? (action as Record<string, unknown>)[extension] : undefined
  if (typeof value === 'string' || typeof value === 'number') {
    // Tagged by where the key comes from, so an action named `x-page=blocked` never shares a node with one whose
    // `x-page` is `blocked`; actions with the same extension value still share theirs.
    return { key: `extension:${extension}=${value}`, label: String(value), named: true }
  }
  return { key: `name:${action.name ?? ''}`, label: action.name || 'Failed', named: false }
}

type Scope = {
  /** Prepended to every node id of the scope, so the same step can appear at several levels. */
  prefix: string
  /** The group the scope's nodes are drawn in. */
  group?: string
  /**
   * The workflows on the expansion path, the drawn one first, as `<document>#<workflowId>`: expanding one of them
   * again would never end. Keyed by document too, so a workflow of another document with the same id is no cycle.
   */
  path: string[]
  /** The workspace name of the document the scope's workflow is in (`options.documentName`, or empty, for the one drawn). */
  documentKey: string
}

/**
 * Builds the overview of a workflow's steps, the same graph the in-core Scalar POC drew with Mermaid.
 *
 * A step's actions are the ones Arazzo runs: its `onSuccess`/`onFailure` plus the workflow's
 * `successActions`/`failureActions` it doesn't override by name, Reusable Objects resolved.
 *
 * Two kinds of arrows, never drawn twice for the same pair of steps:
 * - Actions give labeled arrows to their `goto`/`retry` step or workflow, or to an end node for `end`.
 *   They are the only conditional arrows: `dependsOn` records prerequisites, not which branch is taken.
 * - Everything else follows `dependsOn`, labeled with the source step's `successCriteria` when that
 *   step branches, so every arrow out of a branch point reads the same way. When no step declares
 *   `dependsOn`, the steps are chained in document order, the order Arazzo runs them in by default,
 *   except after a step whose success action always applies (no criteria): that action decides instead.
 *
 * Workflows are nodes of their own: a step that invokes one, and one node per workflow that `goto`/`retry`
 * actions invoke (shared by every step that does). A `retry` through another step or a workflow also gets
 * an arrow back to the step, since the step runs again once that is done. A workflow node can be expanded
 * (`options.expanded`) when its workflow is found: in this document, or for `$sourceDescriptions.<name>.<id>` in
 * the Arazzo document that source names, when it is among `options.documents`. Its steps are then drawn in a
 * group, resolved in their own document, recursively, except a workflow already on the expansion path (the same
 * workflow of the same document), which stays collapsed and is marked as a cycle.
 *
 * `end` success actions lead to one Success node. `end` failure actions lead to one Failed node while they
 * cannot be told apart (one name, no end-label extension); otherwise each gets its own end node, named after
 * the action or the value of `options.endLabelExtension`.
 *
 * A retry with neither `stepId` nor `workflowId` retries the step itself: rather than a self-loop, the step
 * carries it in `retries`, drawn as a badge with its limit and delay.
 */
export const buildWorkflowGraph = (
  document: ArazzoDocument,
  workflow: ArazzoWorkflow | undefined,
  specOf?: (step: ArazzoStep, document: ArazzoDocument) => SpecFamily | undefined,
  options: WorkflowGraphOptions = {},
): WorkflowGraph => {
  const graph: WorkflowGraph = { nodes: [], edges: [], groups: [], bands: [] }
  if (!workflow?.steps?.length) {
    return graph
  }
  const documentKey = options.documentName ?? ''
  addScope(graph, document, workflow, { prefix: '', path: [`${documentKey}#${workflow.workflowId}`], documentKey }, specOf, options)
  return graph
}

/** Adds one workflow's steps, arrows and end nodes to the graph, then the scopes of its expanded workflow nodes. */
const addScope = (
  graph: WorkflowGraph,
  document: ArazzoDocument,
  workflow: ArazzoWorkflow,
  scope: Scope,
  specOf: ((step: ArazzoStep, document: ArazzoDocument) => SpecFamily | undefined) | undefined,
  options: WorkflowGraphOptions,
): string[] => {
  const steps = workflow.steps ?? []
  const { prefix, group } = scope
  const at = (id: string) => `${prefix}${id}`
  const inGroup = <T extends WorkflowGraphNode>(node: T): T => (group ? { ...node, group } : node)
  const workflowNodes: WorkflowGraphNode[] = []

  // Pages view: plain-language labels and pages, from the configured extensions. Node ids stay those of the
  // Technical view, so what is expanded, and where the view is panned, carries over when switching views.
  const inPages = options.view === 'pages'
  const config: PagesConfig = { ...options.pages, endLabelExtension: options.endLabelExtension ?? options.pages?.endLabelExtension }
  const registry = inPages ? pageRegistry(document, config) : {}
  const plainLabel = (of: object | undefined): string | undefined => (inPages ? labelOf(of, config) : undefined)
  /** What a workflow is called in the Pages view: its label, else its summary. */
  const workflowName = (workflowId: string): string | undefined => {
    const target = resolveWorkflow(document, workflowId, options.documents)?.workflow
    return target ? (labelOf(target, config) ?? target.summary) : undefined
  }
  /** Where a workflow this scope invokes is, and its key on the expansion path. */
  const lookUp = (workflowId: string) => {
    const resolved = resolveWorkflow(document, workflowId, options.documents)
    const documentKey = resolved?.documentName ?? scope.documentKey
    return resolved ? { ...resolved, documentKey, key: `${documentKey}#${resolved.workflow.workflowId}` } : undefined
  }
  const edgeFor = (from: string, to: string, kind: 'success' | 'failure', action: ArazzoSuccessAction | ArazzoFailureAction, extra?: string) =>
    actionEdge(from, to, kind, action, extra, plainLabel(action))

  /** A node for a workflow this scope invokes; expandable when it is found, in this document or another loaded one. */
  const workflowNode = (id: string, label: string, workflowId: string, extra: Partial<WorkflowGraphNode> = {}): WorkflowGraphNode => {
    const target = lookUp(workflowId)
    const cycle = Boolean(target && scope.path.includes(target.key))
    const expandable = Boolean(target && !cycle && target.workflow.steps?.length)
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

  const stepNodes = new Map<string, WorkflowGraphNode>()
  for (const step of steps) {
    const spec = specOf?.(step, document)
    const pages = inPages ? { technical: step.stepId, ...pageField(pageOf(step, config)) } : {}
    const node: WorkflowGraphNode = step.workflowId
      ? workflowNode(
          at(step.stepId),
          inPages ? (plainLabel(step) ?? workflowName(step.workflowId) ?? step.stepId) : step.stepId,
          step.workflowId,
          { stepId: step.stepId, ...(spec ? { spec } : {}), ...pages },
        )
      : inGroup({
          id: at(step.stepId),
          label: (inPages && plainLabel(step)) || step.stepId,
          kind: 'step',
          stepId: step.stepId,
          ...(spec ? { spec } : {}),
          ...pages,
        })
    stepNodes.set(step.stepId, node)
    graph.nodes.push(node)
  }

  // Only the actions that can run: none after the first one without criteria.
  const successActions = new Map(steps.map((step) => [step.stepId, reachableActions(effectiveSuccessActions(document, workflow, step))]))
  const failureActions = new Map(steps.map((step) => [step.stepId, reachableActions(effectiveFailureActions(document, workflow, step))]))
  /** Steps whose success always leads where one of their actions says, never on to the next step. */
  const decidesOnSuccess = (step: ArazzoStep): boolean => successActions.get(step.stepId)!.some(isUnconditional)
  const hasActions = (step: ArazzoStep): boolean =>
    Boolean(successActions.get(step.stepId)!.length || failureActions.get(step.stepId)!.length)

  const stepIds = new Set(steps.map((step) => step.stepId))
  const explicit = new Set<string>()
  const actionEdges: WorkflowGraphEdge[] = []
  const invoked = new Map<string, WorkflowGraphNode>()
  const invokedNode = (workflowId: string): WorkflowGraphNode => {
    let node = invoked.get(workflowId)
    if (!node) {
      node = workflowNode(
        workflowNodeId(prefix, workflowId),
        (inPages && workflowName(workflowId)) || workflowLabel(workflowId),
        workflowId,
        { ...(specOf ? { spec: 'arazzo' } : {}), ...(inPages ? { technical: workflowId } : {}) },
      )
      invoked.set(workflowId, node)
    }
    return node
  }
  let usesSuccess = false
  /** Pages view: every arrow into an end node, with the page and caption its action lands on. */
  const landings: { edge: WorkflowGraphEdge; page?: string; caption?: string }[] = []
  /** End failure actions, with their arrow already in place (document order); it learns its end node below. */
  const failureEnds: { from: string; action: ArazzoFailureAction; edge: WorkflowGraphEdge }[] = []

  for (const step of steps) {
    const from = at(step.stepId)
    for (const action of successActions.get(step.stepId)!) {
      if (action.type === 'end') {
        usesSuccess = true
        const edge = edgeFor(from, at(SUCCESS_NODE_ID), 'success', action)
        actionEdges.push(edge)
        landings.push({ edge, page: pageOf(action, config, true) })
      } else if (action.stepId && stepIds.has(action.stepId)) {
        explicit.add(`${step.stepId}->${action.stepId}`)
        actionEdges.push(edgeFor(from, at(action.stepId), 'success', action))
      } else if (action.workflowId) {
        actionEdges.push(edgeFor(from, invokedNode(action.workflowId).id, 'success', action))
      }
    }
    for (const action of failureActions.get(step.stepId)!) {
      if (action.type === 'end') {
        const edge = edgeFor(from, '', 'failure', action)
        actionEdges.push(edge)
        failureEnds.push({ from, action, edge })
        landings.push({ edge, page: pageOf(action, config, true), caption: plainLabel(action) })
      } else if (action.type === 'retry' && !action.workflowId && (!action.stepId || action.stepId === step.stepId)) {
        const node = stepNodes.get(step.stepId)!
        node.retries = [...(node.retries ?? []), selfRetry(action)]
      } else if (action.stepId && stepIds.has(action.stepId)) {
        explicit.add(`${step.stepId}->${action.stepId}`)
        actionEdges.push(edgeFor(from, at(action.stepId), 'failure', action, retryExtra(action)))
        if (action.type === 'retry') {
          actionEdges.push({ from: at(action.stepId), to: from, kind: 'return' })
        }
      } else if (action.workflowId) {
        const target = invokedNode(action.workflowId).id
        actionEdges.push(edgeFor(from, target, 'failure', action, retryExtra(action)))
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
  for (const { action, end, edge } of ends) {
    const id = separate ? endNodeId(prefix, end.key) : at(FAILURE_NODE_ID)
    let node = endNodes.get(id)
    if (!node) {
      node = inGroup({ id, label: separate ? end.label : 'Failed', kind: 'failure', ...(separate ? { actions: [] } : {}) })
      endNodes.set(id, node)
    }
    if (node.actions && action.name && !node.actions.includes(action.name)) {
      node.actions.push(action.name)
    }
    edge.to = id
  }

  const nextEdges: WorkflowGraphEdge[] = []
  const usesDependsOn = steps.some((step) => step.dependsOn?.length)
  if (usesDependsOn) {
    for (const step of steps) {
      for (const dependency of step.dependsOn ?? []) {
        const source = steps.find((candidate) => candidate.stepId === dependency)
        // As in document order: a dependency whose success action always applies goes where that action
        // says, so drawing this step as what runs next would be wrong.
        if (!source || explicit.has(`${dependency}->${step.stepId}`) || decidesOnSuccess(source)) {
          continue
        }
        const label = hasActions(source) ? describeCriteria(source.successCriteria) : undefined
        nextEdges.push({ from: at(dependency), to: at(step.stepId), label, kind: 'next' })
      }
    }
  } else {
    for (let index = 1; index < steps.length; index++) {
      if (!decidesOnSuccess(steps[index - 1]!)) {
        nextEdges.push({ from: at(steps[index - 1]!.stepId), to: at(steps[index]!.stepId), kind: 'next' })
      }
    }
  }

  // Arazzo ends a workflow successfully after its last step: draw that for every step that nothing runs after
  // (the last step, or one no step depends on), unless one of its success actions always applies instead.
  const dependedOn = new Set(steps.flatMap((step) => step.dependsOn ?? []))
  const lastSteps = usesDependsOn ? steps.filter((step) => !dependedOn.has(step.stepId)) : steps.slice(-1)
  const implicitEdges: WorkflowGraphEdge[] = []
  for (const step of lastSteps) {
    if (decidesOnSuccess(step)) {
      continue
    }
    usesSuccess = true
    const criteria = describeCriteria(step.successCriteria)
    const base: WorkflowGraphEdge = { from: at(step.stepId), to: at(SUCCESS_NODE_ID), kind: 'success' }
    const edge = inPages ? { ...base, label: DONE_LABEL } : criteria && criteria.length <= MAX_EDGE_LABEL_LENGTH ? { ...base, label: criteria } : base
    implicitEdges.push(edge)
    landings.push({ edge })
  }

  graph.nodes.push(...[...invoked.values()])
  const successNode: WorkflowGraphNode | undefined = usesSuccess ? inGroup({ id: at(SUCCESS_NODE_ID), label: 'Success', kind: 'success' }) : undefined
  if (successNode) {
    graph.nodes.push(successNode)
  }
  graph.nodes.push(...endNodes.values())

  if (inPages) {
    // An end node names the page it shows only when every arrow into it lands there; otherwise it stays
    // generic and each arrow names its own page, so no outcome borrows another's destination.
    for (const node of [...(successNode ? [successNode] : []), ...endNodes.values()]) {
      const incoming = landings.filter(({ edge }) => edge.to === node.id)
      const pages = new Set(incoming.map(({ page }) => page))
      const [page] = pages
      if (pages.size === 1 && page !== undefined) {
        node.label = node.kind === 'success' ? `${pageTitle(registry, page)} ✓` : pageTitle(registry, page)
        node.page = page
        const captions = new Set(incoming.map(({ caption }) => caption))
        const [caption] = captions
        if (captions.size === 1 && caption) {
          node.subtitle = caption
        }
        continue
      }
      for (const landing of incoming) {
        if (landing.page !== undefined) {
          landing.edge.label = [landing.edge.label, `→ ${pageTitle(registry, landing.page)}`].filter(Boolean).join(' ')
          landing.edge.page = landing.page
        }
      }
    }
  }
  graph.edges.push(...nextEdges, ...actionEdges, ...implicitEdges)

  if (inPages) {
    // Consecutive steps on the same page (or on none) share a band.
    for (const step of steps) {
      const page = pageOf(step, config)
      const last = graph.bands[graph.bands.length - 1]
      if (last && last.id.startsWith(`${prefix}__band__:`) && last.group === group && last.pageId === page) {
        last.nodes.push(at(step.stepId))
        continue
      }
      const band: WorkflowGraphBand = {
        id: `${prefix}__band__:${graph.bands.filter((candidate) => candidate.id.startsWith(`${prefix}__band__:`)).length}`,
        title: page ? pageTitle(registry, page) : NO_PAGE_TITLE,
        nodes: [at(step.stepId)],
        ...(page ? { pageId: page } : {}),
        ...(page && registry[page]?.route ? { route: registry[page]!.route } : {}),
        ...(group ? { group } : {}),
      }
      graph.bands.push(band)
    }
  }

  for (const node of workflowNodes) {
    const nested = node.expanded && node.workflowId ? lookUp(node.workflowId) : undefined
    if (!nested) {
      continue
    }
    const nestedGroup: WorkflowGraphGroup = {
      id: groupId(node.id),
      label: (inPages && workflowName(node.workflowId!)) || workflowLabel(node.workflowId!),
      workflowId: node.workflowId!,
      ...(group ? { parent: group } : {}),
      ...(nested.documentKey !== (options.documentName ?? '') ? { documentName: nested.documentKey } : {}),
    }
    graph.groups.push(nestedGroup)
    // The nested workflow's components, page registry and step targets resolve in its own document.
    const entries = addScope(
      graph,
      nested.document,
      nested.workflow,
      { prefix: `${node.id}/`, group: nestedGroup.id, path: [...scope.path, nested.key], documentKey: nested.documentKey },
      specOf,
      options,
    )
    graph.edges.push(...entries.map((entry) => ({ from: node.id, to: entry, kind: 'expand' as const })))

    // Control comes back to a retrying step once the retried workflow succeeds: start the arrow back at the
    // expanded workflow's own Success node, so that node doesn't read as the end of this workflow.
    const nestedSuccess = `${node.id}/${SUCCESS_NODE_ID}`
    if (graph.nodes.some((candidate) => candidate.id === nestedSuccess)) {
      for (const edge of graph.edges) {
        if (edge.kind === 'return' && edge.from === node.id) {
          edge.from = nestedSuccess
        }
      }
    }
  }

  // The steps a run of this workflow starts at: those without prerequisites, or the first one.
  const entrySteps = usesDependsOn ? steps.filter((step) => !step.dependsOn?.length) : steps.slice(0, 1)
  return entrySteps.map((step) => at(step.stepId))
}

export type Point = { x: number; y: number }

export type LaidOutNode = WorkflowGraphNode & { x: number; y: number; width: number; height: number }

export type LaidOutEdge = WorkflowGraphEdge & { points: Point[]; labelPosition?: Point; labelWidth: number }

export type LaidOutGroup = WorkflowGraphGroup & { x: number; y: number; width: number; height: number }

export type LaidOutBand = WorkflowGraphBand & {
  x: number
  y: number
  width: number
  height: number
  /** The page title and route as drawn: cut with "…" past {@link MAX_BAND_LABEL_LENGTH}, in full in the tooltip. */
  label: string
}

export type WorkflowLayout = {
  width: number
  height: number
  nodes: LaidOutNode[]
  edges: LaidOutEdge[]
  /** Pages view: the page bands, behind the steps they hold. */
  bands: LaidOutBand[]
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
/** Where a group's label starts, from the group's left edge. */
export const GROUP_LABEL_INSET = 10
/** Matches the group label's font size in `ArazzoDocument.vue`. */
const GROUP_LABEL_CHAR_WIDTH = 6.7
/** Room above a band's steps for its page title. */
export const BAND_LABEL_HEIGHT = 18
/** Matches a band title's font size in `ArazzoDocument.vue`. */
const BAND_CHAR_WIDTH = 6
/** Room around a band's steps, and on either side of its title. */
const BAND_PADDING = 8
/** A band's title and route are cut past this, so one long route cannot push the diagram far sideways. */
export const MAX_BAND_LABEL_LENGTH = 64

/** "Checkout · /checkout/payment": what a band's top line says, cut to {@link MAX_BAND_LABEL_LENGTH}. */
const bandLabel = (band: WorkflowGraphBand): string => {
  const full = [band.title, band.route].filter(Boolean).join(' · ')
  return full.length > MAX_BAND_LABEL_LENGTH ? `${full.slice(0, MAX_BAND_LABEL_LENGTH - 1)}…` : full
}

/** How wide a band must be for its title to fit. */
const bandLabelWidth = (band: WorkflowGraphBand): number => bandLabel(band).length * BAND_CHAR_WIDTH + 2 * BAND_PADDING
/** A box with a second line (an end node's subtitle in the Pages view) is taller. */
const SUBTITLE_NODE_HEIGHT = 48
const nodeHeight = (node: WorkflowGraphNode): number => (node.subtitle ? SUBTITLE_NODE_HEIGHT : NODE_HEIGHT)
/** Long conditions are cut, so a single arrow cannot stretch the whole diagram sideways. */
export const MAX_EDGE_LABEL_LENGTH = 48

export const truncateLabel = (label: string): string =>
  label.length > MAX_EDGE_LABEL_LENGTH ? `${label.slice(0, MAX_EDGE_LABEL_LENGTH - 1)}…` : label

/**
 * The badge a step that retries itself carries: "↻ ≤ 3× 2s" for one retry, "↻ 2 retries" for several (the
 * tooltip lists them). `undefined` for a step without one.
 */
export const retryBadge = (node: WorkflowGraphNode): string | undefined => {
  const retries = node.retries ?? []
  if (retries.length > 1) {
    return `↻ ${retries.length} retries`
  }
  const [retry] = retries
  if (!retry) {
    return undefined
  }
  return ['↻', typeof retry.retryLimit === 'number' ? `≤ ${retry.retryLimit}×` : '', typeof retry.retryAfter === 'number' ? `${retry.retryAfter}s` : '']
    .filter(Boolean)
    .join(' ')
}

/** The width of a step's retry badge, drawn inside the right end of its box; 0 without one. */
export const retryBadgeWidth = (node: WorkflowGraphNode): number => {
  const badge = retryBadge(node)
  return badge ? badge.length * LABEL_CHAR_WIDTH + 14 : 0
}

const nodeWidth = (node: WorkflowGraphNode): number =>
  node.kind === 'step'
    ? Math.max(96, node.label.length * NODE_CHAR_WIDTH + 28) + (node.spec ? SPEC_MARKER_WIDTH : 0) + retryBadgeWidth(node)
    : node.kind === 'workflow'
      ? Math.max(96, node.label.length * NODE_CHAR_WIDTH + 28) +
        (node.spec ? SPEC_MARKER_WIDTH : 0) +
        (node.expandable || node.cycle ? WORKFLOW_TOGGLE_WIDTH : 0) +
        retryBadgeWidth(node)
      : Math.max(84, Math.max(node.label.length, (node.subtitle?.length ?? 0) * 0.9) * NODE_CHAR_WIDTH + 28)

/** Room around an expanded workflow's steps inside its group box. */
const GROUP_PADDING = 12

type Box = { x: number; y: number; width: number; height: number }

/** One level of the layout: the top-level workflow, or the steps of one expanded workflow node. */
type ScopeLayout = {
  width: number
  height: number
  /** Node id → its box, relative to the scope's top-left corner. */
  boxes: Map<string, Box>
  /** Edge index → its route and label, relative to the scope's top-left corner. */
  edges: Map<number, { points: Point[]; labelPosition?: Point; labelWidth: number }>
  /** Group id → its box, relative to the scope's top-left corner. */
  groups: Map<string, Box>
  /**
   * Node id → the room laid out for it, when wider than its box: a step's page band, title included, which
   * neighbours and the enclosing group keep clear of.
   */
  room: Map<string, Box>
}

/** Which way an arrow leaves its box: failure paths left, then next steps and gotos, Success rightmost. */
const sidePriority = (edge: WorkflowGraphEdge): number => {
  if (edge.kind === 'failure') {
    return 0
  }
  if (edge.kind === 'expand') {
    return 1
  }
  return edge.kind === 'success' && edge.to.endsWith(SUCCESS_NODE_ID) ? 3 : 2
}

/**
 * Numbers the boxes of one level left to right: a depth-first walk from the entry boxes (the level's head first)
 * that takes each box's arrows by {@link sidePriority}, keeping document order within a priority. Arrows back to
 * an earlier box (`return`) don't lead anywhere new, so the walk skips them.
 */
const orderSlots = (
  layout: dagre.graphlib.Graph,
  edges: { edge: WorkflowGraphEdge; from: string; to: string }[],
  head: string | undefined,
): Map<string, number> => {
  const forward = edges.filter(({ edge }) => edge.kind !== 'return')
  const outgoing = new Map<string, { edge: WorkflowGraphEdge; to: string }[]>()
  const hasIncoming = new Set<string>()
  for (const { edge, from, to } of forward) {
    outgoing.set(from, [...(outgoing.get(from) ?? []), { edge, to }])
    hasIncoming.add(to)
  }
  const slots = new Map<string, number>()
  const visit = (id: string) => {
    if (slots.has(id)) {
      return
    }
    slots.set(id, slots.size)
    const next = [...(outgoing.get(id) ?? [])]
      .map((entry, index) => ({ ...entry, index }))
      .sort((a, b) => sidePriority(a.edge) - sidePriority(b.edge) || a.index - b.index)
    for (const { to } of next) {
      visit(to)
    }
  }
  if (head) {
    visit(head)
  }
  for (const id of layout.nodes()) {
    if (!hasIncoming.has(id)) {
      visit(id)
    }
  }
  for (const id of layout.nodes()) {
    visit(id)
  }
  return slots
}

const shift = (point: Point, dx: number, dy: number): Point => ({ x: point.x + dx, y: point.y + dy })

/**
 * Keeps an arrow's ends on the boxes it joins: a band's step is laid out wider than its box, so dagre can end a
 * slanted arrow on the edge of that room, beside the box.
 */
const onBoxes = (points: Point[], from: Box | undefined, to: Box | undefined): Point[] => {
  if (points.length < 2) {
    return points
  }
  const clamp = (point: Point, box: Box | undefined): Point =>
    box && Math.abs(point.y - box.y) >= box.height / 2 - 1
      ? { ...point, x: Math.min(Math.max(point.x, box.x - box.width / 2 + 4), box.x + box.width / 2 - 4) }
      : point
  return points.map((point, index) => (index === 0 ? clamp(point, from) : index === points.length - 1 ? clamp(point, to) : point))
}

/**
 * Lays the graph out top-down with dagre, the layout engine Mermaid's flowcharts use.
 *
 * An expanded workflow is laid out on its own, its node on top and its steps below it, and that whole block
 * takes the node's place in the level above. The level above therefore has the same nodes and arrows whether
 * the workflow is expanded or not, and since dagre orders a row by its arrows alone (not by the sizes of the
 * boxes), expanding or collapsing a workflow never moves the boxes beside it to the other side.
 */
export const layoutWorkflowGraph = (graph: WorkflowGraph): WorkflowLayout => {
  const groups = graph.groups ?? []
  const nodesById = new Map(graph.nodes.map((node) => [node.id, node]))
  /** Group id → the expanded workflow node it belongs to. */
  const ownerOf = new Map<string, string>()
  for (const node of graph.nodes) {
    if (node.expanded && groups.some((group) => group.id === groupId(node.id))) {
      ownerOf.set(groupId(node.id), node.id)
    }
  }
  const scopeOfNode = (id: string): string => nodesById.get(id)?.group ?? ''

  /**
   * Pages view: the width a band's step takes in the layout, so the band around it, centered on the step and
   * wide enough for its title, fits between its neighbours and inside its group. The steps of one band are
   * mostly stacked, so each reserves the band's width.
   */
  const bandRoom = new Map<string, number>()
  for (const band of graph.bands ?? []) {
    for (const id of band.nodes) {
      const node = nodesById.get(id)
      if (node) {
        bandRoom.set(id, Math.max(nodeWidth(node) + 2 * BAND_PADDING, bandLabelWidth(band)))
      }
    }
  }

  /** Where a node takes part in the layout, innermost level first: [level, the box that stands for it there]. */
  const pathOf = (id: string): [string, string][] => {
    const path: [string, string][] = []
    if (ownerOf.has(groupId(id))) {
      path.push([groupId(id), id])
    }
    let scope = scopeOfNode(id)
    let standIn = id
    for (;;) {
      path.push([scope, standIn])
      const owner = scope ? ownerOf.get(scope) : undefined
      if (!owner) {
        return path
      }
      standIn = owner
      scope = scopeOfNode(owner)
    }
  }

  /** Each edge is laid out on the innermost level that holds both of its ends (or the blocks they are in). */
  const edgesByScope = new Map<string, { index: number; from: string; to: string }[]>()
  graph.edges.forEach((edge, index) => {
    const fromPath = pathOf(edge.from)
    const toPath = pathOf(edge.to)
    for (const [scope, from] of fromPath) {
      const match = toPath.find(([candidate]) => candidate === scope)
      if (match) {
        // An arrow between two boxes inside the same expanded workflow stands for nothing at this level; a step's
        // own arrow to itself (a `goto` back to it) is a real loop, which dagre routes.
        if (match[1] !== from || edge.from === edge.to) {
          const list = edgesByScope.get(scope) ?? []
          list.push({ index, from, to: match[1] })
          edgesByScope.set(scope, list)
        }
        return
      }
    }
  })

  const layoutScope = (scope: string): ScopeLayout => {
    const owner = scope ? ownerOf.get(scope) : undefined
    const members = graph.nodes.filter((node) => (node.group ?? '') === scope)
    const blocks = new Map<string, ScopeLayout>()
    for (const node of members) {
      if (ownerOf.has(groupId(node.id))) {
        blocks.set(node.id, layoutScope(groupId(node.id)))
      }
    }

    const layout = new dagre.graphlib.Graph({ multigraph: true })
    const margin = owner ? GROUP_PADDING : 8
    // Bands put a page title above their steps: leave room for it between rows.
    const ranksep = graph.bands?.length ? 44 + BAND_LABEL_HEIGHT : 44
    layout.setGraph({ rankdir: 'TB', nodesep: 32, ranksep, edgesep: 16, marginx: margin, marginy: owner ? 0 : margin + (graph.bands?.length ? BAND_LABEL_HEIGHT : 0) })
    layout.setDefaultEdgeLabel(() => ({}))
    if (owner) {
      // The expanded workflow's own node heads its block.
      layout.setNode(owner, { width: nodeWidth(nodesById.get(owner)!), height: NODE_HEIGHT })
    }
    for (const node of members) {
      const block = blocks.get(node.id)
      layout.setNode(
        node.id,
        block ? { width: block.width, height: block.height } : { width: Math.max(nodeWidth(node), bandRoom.get(node.id) ?? 0), height: nodeHeight(node) },
      )
    }
    for (const { index, from, to } of edgesByScope.get(scope) ?? []) {
      const edge = graph.edges[index]!
      const label = edge.label ? truncateLabel(edge.label) : ''
      const options: Record<string, unknown> = label ? { width: label.length * LABEL_CHAR_WIDTH + 12, height: LABEL_HEIGHT, labelpos: 'c' } : {}
      // Leave room under the workflow's node for the group's label.
      if (owner && from === owner && edge.kind === 'expand') {
        options.minlen = 2
      }
      layout.setEdge(from, to, options, String(index))
    }

    // Fix the order of each row instead of letting dagre minimise crossings, so it is the same in every render:
    // number the boxes by walking from the entry steps, taking each box's failure paths first (in document
    // order), then its next steps and gotos, then Success; then sort every row by those numbers.
    const slots = orderSlots(layout, (edgesByScope.get(scope) ?? []).map(({ index, from, to }) => ({ edge: graph.edges[index]!, from, to })), owner)
    const slotOf = (id: string, node: { edgeObj?: { v: string; w: string } }): number => {
      if (slots.has(id)) {
        return slots.get(id)!
      }
      // A dummy box dagre adds for a long arrow or its label: keep it beside the box the arrow leads to.
      return node.edgeObj ? (slots.get(node.edgeObj.w) ?? 0) - 0.5 + (slots.get(node.edgeObj.v) ?? 0) / 1e6 : Number.MAX_SAFE_INTEGER
    }
    dagre.layout(layout, {
      customOrder: (internal: dagre.graphlib.Graph) => {
        const rows = new Map<number, string[]>()
        for (const id of internal.nodes()) {
          const rank = internal.node(id)?.rank
          if (typeof rank === 'number') {
            rows.set(rank, [...(rows.get(rank) ?? []), id])
          }
        }
        for (const row of rows.values()) {
          row
            .sort((a, b) => slotOf(a, internal.node(a) as never) - slotOf(b, internal.node(b) as never))
            .forEach((id, index) => {
              // `order` is the attribute dagre's own ordering phase sets; its typings leave it out.
              ;(internal.node(id) as { order?: number }).order = index
            })
        }
      },
    } as never)

    const result: ScopeLayout = { width: 0, height: 0, boxes: new Map(), edges: new Map(), groups: new Map(), room: new Map() }
    for (const id of layout.nodes()) {
      const placed = layout.node(id)
      const block = blocks.get(id)
      if (!block) {
        // A band's step was laid out as wide as its band: draw the box at its own width, and keep the room.
        const width = bandRoom.has(id) ? nodeWidth(nodesById.get(id)!) : placed.width
        result.boxes.set(id, { x: placed.x, y: placed.y, width, height: placed.height })
        if (width !== placed.width) {
          result.room.set(id, { x: placed.x, y: placed.y, width: placed.width, height: placed.height })
        }
        continue
      }
      // Place the block's contents where dagre put the block.
      const dx = placed.x - block.width / 2
      const dy = placed.y - block.height / 2
      for (const [inner, box] of block.boxes) {
        result.boxes.set(inner, { ...box, ...shift(box, dx, dy) })
      }
      for (const [inner, box] of block.groups) {
        result.groups.set(inner, { ...box, ...shift(box, dx, dy) })
      }
      for (const [inner, box] of block.room) {
        result.room.set(inner, { ...box, ...shift(box, dx, dy) })
      }
      for (const [index, route] of block.edges) {
        result.edges.set(index, {
          ...route,
          points: route.points.map((point) => shift(point, dx, dy)),
          labelPosition: route.labelPosition ? shift(route.labelPosition, dx, dy) : undefined,
        })
      }
    }
    for (const { index, from, to } of edgesByScope.get(scope) ?? []) {
      const edge = graph.edges[index]!
      const placed = layout.edge({ v: from, w: to, name: String(index) }) as
        | { points?: Point[]; x?: number; y?: number; width?: number }
        | undefined
      result.edges.set(index, {
        points: onBoxes(placed?.points ?? [], result.boxes.get(from), result.boxes.get(to)),
        labelPosition: edge.label && typeof placed?.x === 'number' && typeof placed?.y === 'number' ? { x: placed.x, y: placed.y } : undefined,
        labelWidth: placed?.width ?? 0,
      })
    }

    const info = layout.graph()
    result.width = info.width ?? 0
    result.height = info.height ?? 0
    if (owner) {
      // The group box: around the steps, below the workflow's node, with room for its label on top.
      const head = result.boxes.get(owner)!
      // Around each step's room (its page band) rather than its box, so a band never sticks out of the group.
      const inner = [...result.boxes.entries()].filter(([id]) => id !== owner).map(([id, box]) => result.room.get(id) ?? box)
      const left = Math.min(...inner.map((box) => box.x - box.width / 2)) - GROUP_PADDING
      // Wide enough for the group's label (cut like an arrow's label; the full id is in its tooltip).
      const label = groups.find((candidate) => candidate.id === scope)?.label ?? ''
      const right = Math.max(
        Math.max(...inner.map((box) => box.x + box.width / 2)) + GROUP_PADDING,
        left + GROUP_LABEL_INSET + truncateLabel(label).length * GROUP_LABEL_CHAR_WIDTH + GROUP_PADDING,
      )
      // With page bands, the first band's title sits between the group's label and its steps.
      const bandRoom = graph.bands?.length ? BAND_LABEL_HEIGHT + 4 : 0
      const top = Math.max(head.y + head.height / 2 + 6, Math.min(...inner.map((box) => box.y - box.height / 2)) - GROUP_LABEL_HEIGHT - bandRoom)
      const bottom = Math.max(...inner.map((box) => box.y + box.height / 2)) + GROUP_PADDING
      result.groups.set(scope, { x: (left + right) / 2, y: (top + bottom) / 2, width: right - left, height: bottom - top })
      result.height = Math.max(result.height, bottom)
      result.width = Math.max(result.width, right, head.x + head.width / 2 + GROUP_PADDING)
    }
    return result
  }

  const top = layoutScope('')

  /** Arrows that end at an expanded workflow's node, or start inside its block, are joined to the real box. */
  const attach = (edge: WorkflowGraphEdge, points: Point[]): Point[] => {
    if (points.length < 2) {
      return points
    }
    const result = [...points]
    const target = top.boxes.get(edge.to)
    if (target && ownerOf.has(groupId(edge.to))) {
      const last = result[result.length - 1]!
      const above = last.y <= target.y
      result[result.length - 1] = { x: target.x, y: target.y + (above ? -1 : 1) * (target.height / 2) }
    }
    // An arrow out of an expanded workflow (its next step, say) leaves from under its group, once its steps are
    // done, as dagre routed it; only an arrow from a box inside a group (a return from the inner Success) is
    // joined to that box.
    const source = ownerOf.has(groupId(edge.from)) ? undefined : top.boxes.get(edge.from)
    const first = result[0]!
    if (source && (Math.abs(first.x - source.x) > source.width / 2 + 1 || Math.abs(first.y - source.y) > source.height / 2 + 1)) {
      // Laid out from the block the source is in: start at the source box itself.
      result.unshift({ x: source.x, y: source.y + (first.y >= source.y ? 1 : -1) * (source.height / 2) })
    }
    return result
  }

  return {
    width: top.width,
    height: top.height,
    groups: groups.flatMap((group) => {
      const box = top.groups.get(group.id)
      return box ? [{ ...group, ...box }] : []
    }),
    nodes: graph.nodes.map((node) => ({ ...node, ...(top.boxes.get(node.id) ?? { x: 0, y: 0, width: nodeWidth(node), height: nodeHeight(node) }) })),
    bands: (graph.bands ?? []).flatMap((band) => {
      const members = band.nodes.flatMap((id) => {
        const box = top.boxes.get(id)
        return box ? [{ id, box }] : []
      })
      if (!members.length) {
        return []
      }
      const label = bandLabel(band)
      /**
       * A band around some boxes, with its page title on top: widened evenly on both sides when the title is
       * wider than the boxes, into the room the layout kept for it.
       */
      const around = (boxes: Box[]): Box => {
        const left = Math.min(...boxes.map((box) => box.x - box.width / 2)) - BAND_PADDING
        const right = Math.max(...boxes.map((box) => box.x + box.width / 2)) + BAND_PADDING
        const width = Math.max(right - left, bandLabelWidth(band))
        const topEdge = Math.min(...boxes.map((box) => box.y - box.height / 2)) - BAND_LABEL_HEIGHT - 4
        const bottom = Math.max(...boxes.map((box) => box.y + box.height / 2)) + 6
        return { x: (left + right) / 2, y: (topEdge + bottom) / 2, width, height: bottom - topEdge }
      }
      const covers = (box: Box): boolean =>
        [...top.boxes.entries()].some(
          ([id, other]) =>
            !band.nodes.includes(id) &&
            !ownerOf.has(groupId(id)) &&
            Math.abs(other.x - box.x) < (other.width + box.width) / 2 &&
            Math.abs(other.y - box.y) < (other.height + box.height) / 2,
        )
      const whole = around(members.map((member) => member.box))
      if (!covers(whole)) {
        return [{ ...band, ...whole, label }]
      }
      // The steps are apart, with other boxes between them: one band per step rather than one that would
      // seem to put those boxes on the page too.
      return members.map((member, index) => ({ ...band, id: `${band.id}/${index}`, nodes: [member.id], ...around([member.box]), label }))
    }),
    edges: graph.edges.map((edge, index) => {
      const route = top.edges.get(index)
      return {
        ...edge,
        label: edge.label ? truncateLabel(edge.label) : undefined,
        points: attach(edge, route?.points ?? []),
        labelPosition: route?.labelPosition,
        labelWidth: route?.labelWidth ?? 0,
      }
    }),
  }
}
