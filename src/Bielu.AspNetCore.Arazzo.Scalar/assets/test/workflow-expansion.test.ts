import { describe, expect, it } from 'vitest'
import {
  buildWorkflowGraph,
  endNodeId,
  FAILURE_NODE_ID,
  layoutWorkflowGraph,
  SUCCESS_NODE_ID,
  workflowNodeId,
} from '../src/helpers/workflow-graph'
import { nestedWorkflowSteps } from '../src/helpers/nested-workflow'
import { pluginOptionsFromModuleUrl, resolvePluginOptions } from '../src/helpers/plugin-options'
import type { ArazzoDocument, ArazzoFailureAction, ArazzoStep, ArazzoWorkflow } from '../src/types'

/** A shop's workflows: every API step can send the customer through sign-in and retry, or end on a page. */
const signIn: ArazzoWorkflow = {
  workflowId: 'signIn',
  steps: [{ stepId: 'authorize', operationId: 'authorize' }, { stepId: 'exchangeCode', operationId: 'token' }],
}

const failures = (extra: ArazzoFailureAction[] = []): ArazzoFailureAction[] => [
  {
    name: 'signIn',
    type: 'retry',
    workflowId: 'signIn',
    retryLimit: 1,
    criteria: [{ condition: '$statusCode == 401' }],
    parameters: [{ name: 'returnUrl', value: '$url' }],
  },
  { name: 'showForbiddenPage', type: 'end', criteria: [{ condition: '$statusCode == 403' }], 'x-page': 'forbidden' } as ArazzoFailureAction,
  ...extra,
  { name: 'showErrorPage', type: 'end', 'x-page': 'error' } as ArazzoFailureAction,
]

const browseOrders: ArazzoWorkflow = {
  workflowId: 'browseOrders',
  steps: [
    { stepId: 'loadAccount', workflowId: 'loadAccount' },
    { stepId: 'listOrders', operationId: 'listOrders', onFailure: failures() },
  ],
}

const loadAccount: ArazzoWorkflow = {
  workflowId: 'loadAccount',
  steps: [{ stepId: 'getAccount', operationId: 'getAccount', onFailure: failures() }],
}

const document: ArazzoDocument = { arazzo: '1.1.0', workflows: [browseOrders, loadAccount, signIn] }

const ids = (graph: { nodes: { id: string }[] }) => graph.nodes.map((node) => node.id)

describe('workflow nodes', () => {
  it('draws a step that invokes a workflow of the document as a collapsed workflow node', () => {
    // Act
    const graph = buildWorkflowGraph(document, browseOrders)

    // Assert
    const node = graph.nodes.find((candidate) => candidate.id === 'loadAccount')!
    expect(node).toMatchObject({ kind: 'workflow', label: 'loadAccount', workflowId: 'loadAccount', expandable: true, expanded: false })
    expect(graph.groups).toEqual([])
  })

  it('keeps a step that invokes a workflow of another document an ordinary, unexpandable workflow node', () => {
    // Arrange
    const workflow: ArazzoWorkflow = { workflowId: 'wf', steps: [{ stepId: 'remote', workflowId: '$sourceDescriptions.other.pay' }] }

    // Act
    const graph = buildWorkflowGraph({ arazzo: '1.1.0', workflows: [workflow] }, workflow)

    // Assert
    expect(graph.nodes[0]).toMatchObject({ kind: 'workflow', expandable: false })
  })

  it('draws a retry through a workflow as an arrow to the workflow node and one back to the step', () => {
    // Act
    const graph = buildWorkflowGraph(document, loadAccount)

    // Assert
    const signInNode = workflowNodeId('', 'signIn')
    expect(graph.nodes.find((node) => node.id === signInNode)).toMatchObject({ kind: 'workflow', label: 'signIn', expandable: true })
    expect(graph.edges).toContainEqual({ from: 'getAccount', to: signInNode, kind: 'failure', label: '$statusCode == 401 — retry, ≤ 1×', action: 'signIn' })
    expect(graph.edges).toContainEqual({ from: signInNode, to: 'getAccount', kind: 'return' })
  })

  it('draws a goto to a workflow without an arrow back', () => {
    // Arrange
    const workflow: ArazzoWorkflow = {
      workflowId: 'wf',
      steps: [{ stepId: 'a', onSuccess: [{ name: 'continue', type: 'goto', workflowId: 'signIn' }] }],
    }

    // Act
    const graph = buildWorkflowGraph({ arazzo: '1.1.0', workflows: [workflow, signIn] }, workflow)

    // Assert
    const signInNode = workflowNodeId('', 'signIn')
    expect(graph.edges).toEqual([{ from: 'a', to: signInNode, kind: 'success', label: 'otherwise', action: 'continue' }])
  })

  it('shares one workflow node between every step that retries through the same workflow', () => {
    // Arrange
    const workflow: ArazzoWorkflow = {
      workflowId: 'wf',
      steps: [
        { stepId: 'a', onFailure: failures() },
        { stepId: 'b', onFailure: failures() },
      ],
    }

    // Act
    const graph = buildWorkflowGraph({ arazzo: '1.1.0', workflows: [workflow, signIn] }, workflow)

    // Assert
    expect(graph.nodes.filter((node) => node.kind === 'workflow')).toHaveLength(1)
    expect(graph.edges.filter((edge) => edge.kind === 'return').map((edge) => edge.to)).toEqual(['a', 'b'])
  })
})

describe('expanding workflow nodes', () => {
  it('nests the expanded workflow\'s steps in a group, entered from the workflow node', () => {
    // Act
    const graph = buildWorkflowGraph(document, browseOrders, undefined, { expanded: new Set(['loadAccount']) })

    // Assert
    expect(graph.nodes.find((node) => node.id === 'loadAccount')).toMatchObject({ expanded: true })
    expect(graph.groups).toEqual([{ id: 'loadAccount::group', label: 'loadAccount', workflowId: 'loadAccount' }])
    const nested = graph.nodes.filter((node) => node.group === 'loadAccount::group')
    expect(nested.map((node) => node.id)).toEqual(
      expect.arrayContaining(['loadAccount/getAccount', workflowNodeId('loadAccount/', 'signIn')]),
    )
    expect(graph.edges).toContainEqual({ from: 'loadAccount', to: 'loadAccount/getAccount', kind: 'expand' })
    expect(graph.edges).toContainEqual({ from: workflowNodeId('loadAccount/', 'signIn'), to: 'loadAccount/getAccount', kind: 'return' })
  })

  it('starts the arrow back from an expanded retried workflow at its own Success node', () => {
    // Arrange
    const nestedSignIn = workflowNodeId('loadAccount/', 'signIn')

    // Act
    const graph = buildWorkflowGraph(document, loadAccount, undefined, { expanded: new Set([workflowNodeId('', 'signIn')]) })
    const nested = buildWorkflowGraph(document, browseOrders, undefined, { expanded: new Set(['loadAccount', nestedSignIn]) })

    // Assert: the inner Success is where control goes back to the step, not an end of the outer workflow.
    const signInNode = workflowNodeId('', 'signIn')
    expect(graph.edges.filter((edge) => edge.kind === 'return')).toEqual([
      { from: `${signInNode}/${SUCCESS_NODE_ID}`, to: 'getAccount', kind: 'return' },
    ])
    expect(nested.edges.filter((edge) => edge.kind === 'return' && edge.to === 'loadAccount/getAccount')).toEqual([
      { from: `${nestedSignIn}/${SUCCESS_NODE_ID}`, to: 'loadAccount/getAccount', kind: 'return' },
    ])
  })

  it('keeps the arrow back on the workflow node when the expanded workflow has no Success node', () => {
    // Arrange: the retried workflow always ends on a failure.
    const giveUp: ArazzoWorkflow = { workflowId: 'giveUp', steps: [{ stepId: 'stop', onSuccess: [{ name: 'loop', type: 'goto', stepId: 'stop' }] }] }
    const workflow: ArazzoWorkflow = { workflowId: 'wf', steps: [{ stepId: 'a', onFailure: [{ name: 'again', type: 'retry', workflowId: 'giveUp' }] }] }

    // Act
    const graph = buildWorkflowGraph({ arazzo: '1.1.0', workflows: [workflow, giveUp] }, workflow, undefined, {
      expanded: new Set([workflowNodeId('', 'giveUp')]),
    })

    // Assert
    expect(graph.edges.filter((edge) => edge.kind === 'return')).toEqual([{ from: workflowNodeId('', 'giveUp'), to: 'a', kind: 'return' }])
  })

  it('expands recursively, each level in a group inside its parent\'s', () => {
    // Arrange
    const nestedSignIn = workflowNodeId('loadAccount/', 'signIn')

    // Act
    const graph = buildWorkflowGraph(document, browseOrders, undefined, { expanded: new Set(['loadAccount', nestedSignIn]) })

    // Assert
    expect(graph.groups).toContainEqual({ id: `${nestedSignIn}::group`, label: 'signIn', workflowId: 'signIn', parent: 'loadAccount::group' })
    expect(ids(graph)).toEqual(expect.arrayContaining([`${nestedSignIn}/authorize`, `${nestedSignIn}/exchangeCode`]))
    expect(graph.edges).toContainEqual({ from: `${nestedSignIn}/authorize`, to: `${nestedSignIn}/exchangeCode`, kind: 'next' })
  })

  it('keeps a workflow already on the expansion path collapsed and marks the cycle', () => {
    // Arrange
    const loop: ArazzoWorkflow = { workflowId: 'loop', steps: [{ stepId: 'again', workflowId: 'loop' }] }
    const outer: ArazzoWorkflow = { workflowId: 'outer', steps: [{ stepId: 'enter', workflowId: 'loop' }] }

    // Act
    const graph = buildWorkflowGraph({ arazzo: '1.1.0', workflows: [outer, loop] }, outer, undefined, {
      expanded: new Set(['enter', 'enter/again']),
    })

    // Assert
    expect(graph.nodes.find((node) => node.id === 'enter/again')).toMatchObject({ kind: 'workflow', cycle: true, expandable: false, expanded: false })
    expect(graph.groups).toHaveLength(1)
  })

  it('treats the workflow being drawn as part of the path, so a step invoking it is a cycle', () => {
    // Arrange
    const loop: ArazzoWorkflow = { workflowId: 'loop', steps: [{ stepId: 'again', workflowId: 'loop' }] }

    // Act
    const graph = buildWorkflowGraph({ arazzo: '1.1.0', workflows: [loop] }, loop, undefined, { expanded: new Set(['again']) })

    // Assert
    expect(graph.nodes[0]).toMatchObject({ cycle: true, expanded: false })
  })

  it('keeps the order of a step\'s branches when the workflow one of them invokes is expanded or collapsed', () => {
    // Arrange: a step that retries through signIn, ends on two pages, and succeeds implicitly.
    const workflow: ArazzoWorkflow = { workflowId: 'wf', steps: [{ stepId: 'getAccount', onFailure: failures() }] }
    const doc: ArazzoDocument = { arazzo: '1.1.0', workflows: [workflow, signIn] }
    const signInNode = workflowNodeId('', 'signIn')
    const row = [signInNode, endNodeId('', 'name:showForbiddenPage'), endNodeId('', 'name:showErrorPage'), SUCCESS_NODE_ID]
    const orderOf = (expanded: Set<string>) => {
      const layout = layoutWorkflowGraph(buildWorkflowGraph(doc, workflow, undefined, { expanded }))
      return [...row].sort((a, b) => layout.nodes.find((node) => node.id === a)!.x - layout.nodes.find((node) => node.id === b)!.x)
    }

    // Act
    const collapsed = orderOf(new Set())
    const expanded = orderOf(new Set([signInNode]))
    const collapsedAgain = orderOf(new Set())

    // Assert
    expect(expanded).toEqual(collapsed)
    expect(collapsedAgain).toEqual(collapsed)
  })

  it('puts a step\'s failure paths on the left, in document order, and its success paths on the right', () => {
    // Arrange: the end actions come before the retry in this step's onFailure.
    const workflow: ArazzoWorkflow = {
      workflowId: 'wf',
      steps: [
        {
          stepId: 'getAccount',
          onFailure: [
            { name: 'showForbiddenPage', type: 'end', criteria: [{ condition: '$statusCode == 403' }] },
            { name: 'signIn', type: 'retry', workflowId: 'signIn', criteria: [{ condition: '$statusCode == 401' }] },
            { name: 'showErrorPage', type: 'end' },
          ],
          onSuccess: [{ name: 'more', type: 'goto', stepId: 'next', criteria: [{ condition: '$response.body#/more' }] }],
        },
        { stepId: 'next', dependsOn: ['getAccount'] },
      ],
    }
    const doc: ArazzoDocument = { arazzo: '1.1.0', workflows: [workflow, signIn] }

    // Act
    const layout = layoutWorkflowGraph(buildWorkflowGraph(doc, workflow))

    // Assert: the row under getAccount, left to right.
    const row = [endNodeId('', 'name:showForbiddenPage'), workflowNodeId('', 'signIn'), endNodeId('', 'name:showErrorPage'), 'next']
    const xs = row.map((id) => layout.nodes.find((node) => node.id === id)!.x)
    expect(xs).toEqual([...xs].sort((a, b) => a - b))
  })

  it('orders the row under a step the same way inside an expanded workflow, and keeps Success rightmost', () => {
    // Arrange: signing in can fail at exchangeCode.
    const failingSignIn: ArazzoWorkflow = {
      workflowId: 'signIn',
      steps: [
        { stepId: 'authorize' },
        { stepId: 'exchangeCode', onFailure: [{ name: 'tryAgain', type: 'end', criteria: [{ condition: '$statusCode == 429' }] }, { name: 'signInFailed', type: 'end' }] },
      ],
    }
    const workflow: ArazzoWorkflow = { workflowId: 'wf', steps: [{ stepId: 'getAccount', onFailure: failures() }] }
    const signInNode = workflowNodeId('', 'signIn')

    // Act
    const layout = layoutWorkflowGraph(
      buildWorkflowGraph({ arazzo: '1.1.0', workflows: [workflow, failingSignIn] }, workflow, undefined, { expanded: new Set([signInNode]) }),
    )

    // Assert
    const x = (id: string) => layout.nodes.find((node) => node.id === id)!.x
    expect(x(endNodeId(`${signInNode}/`, 'name:tryAgain'))).toBeLessThan(x(endNodeId(`${signInNode}/`, 'name:signInFailed')))
    expect(x(endNodeId(`${signInNode}/`, 'name:signInFailed'))).toBeLessThan(x(`${signInNode}/${SUCCESS_NODE_ID}`))
    expect(x(signInNode)).toBeLessThan(x(endNodeId('', 'name:showForbiddenPage')))
    expect(x(endNodeId('', 'name:showErrorPage'))).toBeLessThan(x(SUCCESS_NODE_ID))
  })

  it('starts the arrow out of an expanded workflow below its group, not across it', () => {
    // Act
    const layout = layoutWorkflowGraph(buildWorkflowGraph(document, browseOrders, undefined, { expanded: new Set(['loadAccount']) }))

    // Assert
    const group = layout.groups.find((candidate) => candidate.id === 'loadAccount::group')!
    const next = layout.edges.find((edge) => edge.from === 'loadAccount' && edge.to === 'listOrders')!
    expect(next.points[0]!.y).toBeGreaterThanOrEqual(group.y + group.height / 2 - 1)
  })

  it('opens an expanded workflow\'s group below its node', () => {
    // Act
    const layout = layoutWorkflowGraph(buildWorkflowGraph(document, browseOrders, undefined, { expanded: new Set(['loadAccount']) }))

    // Assert
    const node = layout.nodes.find((candidate) => candidate.id === 'loadAccount')!
    const group = layout.groups.find((candidate) => candidate.id === 'loadAccount::group')!
    expect(group.y - group.height / 2).toBeGreaterThan(node.y + node.height / 2)
    expect(Math.abs(group.x - node.x)).toBeLessThan(group.width / 2)
  })

  it('lays groups out around their nodes', () => {
    // Act
    const layout = layoutWorkflowGraph(buildWorkflowGraph(document, browseOrders, undefined, { expanded: new Set(['loadAccount']) }))

    // Assert
    const group = layout.groups.find((candidate) => candidate.id === 'loadAccount::group')!
    const getAccount = layout.nodes.find((node) => node.id === 'loadAccount/getAccount')!
    expect(group.width).toBeGreaterThan(getAccount.width)
    expect(Math.abs(group.x - getAccount.x)).toBeLessThan(group.width / 2)
    expect(Math.abs(group.y - getAccount.y)).toBeLessThan(group.height / 2)
  })
})

describe('end nodes', () => {
  it('keeps one Failed node when every end action has the same name', () => {
    // Arrange
    const workflow: ArazzoWorkflow = {
      workflowId: 'wf',
      steps: [
        { stepId: 'a', onFailure: [{ name: 'giveUp', type: 'end' }] },
        { stepId: 'b', onFailure: [{ name: 'giveUp', type: 'end' }] },
      ],
    }

    // Act
    const graph = buildWorkflowGraph({ arazzo: '1.1.0', workflows: [workflow] }, workflow)

    // Assert
    expect(graph.nodes.filter((node) => node.kind === 'failure')).toEqual([{ id: FAILURE_NODE_ID, label: 'Failed', kind: 'failure' }])
    expect(graph.edges.filter((edge) => edge.to === FAILURE_NODE_ID).map((edge) => edge.from)).toEqual(['a', 'b'])
  })

  it('gives each distinct end action its own end node, named after the action', () => {
    // Act
    const graph = buildWorkflowGraph(document, loadAccount)

    // Assert
    expect(graph.nodes.filter((node) => node.kind === 'failure').map((node) => [node.id, node.label])).toEqual([
      [endNodeId('', 'name:showForbiddenPage'), 'showForbiddenPage'],
      [endNodeId('', 'name:showErrorPage'), 'showErrorPage'],
    ])
    expect(graph.edges).toContainEqual({ from: 'getAccount', to: endNodeId('', 'name:showErrorPage'), kind: 'failure', label: 'otherwise', action: 'showErrorPage' })
  })

  it('labels end nodes with the configured extension when the action has it', () => {
    // Arrange
    const workflow: ArazzoWorkflow = {
      workflowId: 'wf',
      steps: [{ stepId: 'getAccount', onFailure: failures([{ name: 'showNotFound', type: 'end', criteria: [{ condition: '$statusCode == 404' }] }]) }],
    }

    // Act
    const graph = buildWorkflowGraph({ arazzo: '1.1.0', workflows: [workflow, signIn] }, workflow, undefined, {
      endLabelExtension: 'x-page',
    })

    // Assert
    expect(graph.nodes.filter((node) => node.kind === 'failure').map((node) => node.label)).toEqual(['forbidden', 'showNotFound', 'error'])
  })

  it('keeps a single Success node for end success actions', () => {
    // Arrange
    const workflow: ArazzoWorkflow = {
      workflowId: 'wf',
      steps: [{ stepId: 'a', onSuccess: [{ name: 'one', type: 'end' }, { name: 'two', type: 'end' }] }],
    }

    // Act
    const graph = buildWorkflowGraph({ arazzo: '1.1.0', workflows: [workflow] }, workflow)

    // Assert
    expect(ids(graph)).toEqual(['a', SUCCESS_NODE_ID])
  })
})

describe('implicit success', () => {
  it('ends a single-step workflow with only failure actions in Success, labelled with its success criteria', () => {
    // Arrange: no onSuccess, a retry through signIn, two end pages.
    const workflow: ArazzoWorkflow = {
      workflowId: 'loadAccount',
      steps: [{ stepId: 'getAccount', operationId: 'getAccount', successCriteria: [{ condition: '$statusCode == 200' }], onFailure: failures() }],
    }

    // Act
    const graph = buildWorkflowGraph({ arazzo: '1.1.0', workflows: [workflow, signIn] }, workflow)

    // Assert
    expect(ids(graph)).toEqual([
      'getAccount',
      workflowNodeId('', 'signIn'),
      SUCCESS_NODE_ID,
      endNodeId('', 'name:showForbiddenPage'),
      endNodeId('', 'name:showErrorPage'),
    ])
    expect(graph.edges).toEqual([
      { from: 'getAccount', to: workflowNodeId('', 'signIn'), kind: 'failure', label: '$statusCode == 401 — retry, ≤ 1×', action: 'signIn' },
      { from: workflowNodeId('', 'signIn'), to: 'getAccount', kind: 'return' },
      { from: 'getAccount', to: endNodeId('', 'name:showForbiddenPage'), kind: 'failure', label: '$statusCode == 403', action: 'showForbiddenPage' },
      { from: 'getAccount', to: endNodeId('', 'name:showErrorPage'), kind: 'failure', label: 'otherwise', action: 'showErrorPage' },
      { from: 'getAccount', to: SUCCESS_NODE_ID, kind: 'success', label: '$statusCode == 200' },
    ])
  })

  it('leaves the implicit success arrow unlabelled when the criteria are too long to read on it', () => {
    // Arrange
    const workflow: ArazzoWorkflow = {
      workflowId: 'wf',
      steps: [{ stepId: 'a', successCriteria: [{ condition: '$statusCode == 200' }, { condition: '$response.body#/items/0/id != null' }] }],
    }

    // Act
    const graph = buildWorkflowGraph({ arazzo: '1.1.0', workflows: [workflow] }, workflow)

    // Assert
    expect(graph.edges).toEqual([{ from: 'a', to: SUCCESS_NODE_ID, kind: 'success' }])
  })

  it('ends every step nothing depends on, and only the last step of a chain', () => {
    // Arrange
    const workflow: ArazzoWorkflow = {
      workflowId: 'wf',
      steps: [{ stepId: 'a' }, { stepId: 'b', dependsOn: ['a'] }, { stepId: 'c', dependsOn: ['a'], onSuccess: [{ name: 'back', type: 'goto', stepId: 'a' }] }],
    }

    // Act
    const graph = buildWorkflowGraph({ arazzo: '1.1.0', workflows: [workflow] }, workflow)

    // Assert: c decides where it goes itself.
    expect(graph.edges.filter((edge) => edge.to === SUCCESS_NODE_ID).map((edge) => edge.from)).toEqual(['b'])
  })
})

describe('nestedWorkflowSteps', () => {
  it('summarises the steps of a workflow of the document, read-only', () => {
    // Act
    const summary = nestedWorkflowSteps(document, { stepId: 'loadAccount', workflowId: 'loadAccount' } satisfies ArazzoStep)

    // Assert
    expect(summary).toEqual({
      workflowId: 'loadAccount',
      steps: [{ stepId: 'getAccount', target: 'getAccount', kind: 'operation' }],
    })
  })

  it('names the workflows a nested step invokes', () => {
    // Act
    const summary = nestedWorkflowSteps(document, { stepId: 'browse', workflowId: 'browseOrders' })

    // Assert
    expect(summary?.steps[0]).toEqual({ stepId: 'loadAccount', target: 'loadAccount', kind: 'workflow' })
  })

  it('has nothing to show for a workflow of another document or one that does not exist', () => {
    // Act / Assert
    expect(nestedWorkflowSteps(document, { stepId: 'x', workflowId: '$sourceDescriptions.other.pay' })).toBeUndefined()
    expect(nestedWorkflowSteps(document, { stepId: 'x', workflowId: 'missing' })).toBeUndefined()
    expect(nestedWorkflowSteps(document, { stepId: 'x', operationId: 'op' })).toBeUndefined()
  })
})

describe('plugin options', () => {
  it('prefers explicit options, then the page global, then the module URL', () => {
    // Act / Assert
    expect(resolvePluginOptions({ endLabelExtension: 'x-a' }, { endLabelExtension: 'x-b' }, 'https://cdn.test/p.mjs?endLabelExtension=x-c'))
      .toEqual({ endLabelExtension: 'x-a' })
    expect(resolvePluginOptions(undefined, { endLabelExtension: 'x-b' }, 'https://cdn.test/p.mjs?endLabelExtension=x-c'))
      .toEqual({ endLabelExtension: 'x-b' })
    expect(resolvePluginOptions(undefined, undefined, 'https://cdn.test/p.mjs?endLabelExtension=x-c')).toEqual({ endLabelExtension: 'x-c' })
  })

  it('ignores malformed values', () => {
    // Act / Assert
    expect(resolvePluginOptions(undefined, { endLabelExtension: 42 }, 'not a url')).toEqual({})
    expect(pluginOptionsFromModuleUrl(undefined)).toEqual({})
    expect(pluginOptionsFromModuleUrl('https://cdn.test/p.mjs?endLabelExtension=')).toEqual({})
  })

  it('accepts only "x-" followed by a name for every option, from every input path', () => {
    // Arrange: a bare prefix and a non-extension for each of the four options.
    const bad = (value: string) => ({ endLabelExtension: value, pagesExtension: value, pageExtension: value, labelExtension: value })
    const query = (value: string) => `https://cdn.test/p.mjs?${new URLSearchParams(bad(value))}`

    // Act / Assert
    for (const value of ['x-', 'page']) {
      expect(resolvePluginOptions(bad(value))).toEqual({})
      expect(resolvePluginOptions(undefined, bad(value))).toEqual({})
      expect(pluginOptionsFromModuleUrl(query(value))).toEqual({})
    }
  })

  it('reads every option back from a module URL, including one that needed encoding', () => {
    // Act
    const options = pluginOptionsFromModuleUrl(
      'https://cdn.test/p.mjs?v=2&endLabelExtension=x-end&pagesExtension=x-shop%20pages&pageExtension=x-page&labelExtension=x-label%26text#top',
    )

    // Assert
    expect(options).toEqual({ endLabelExtension: 'x-end', pagesExtension: 'x-shop pages', pageExtension: 'x-page', labelExtension: 'x-label&text' })
  })
})
