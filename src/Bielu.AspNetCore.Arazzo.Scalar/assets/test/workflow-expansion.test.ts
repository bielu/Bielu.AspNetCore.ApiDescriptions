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
    expect(graph.edges).toContainEqual({ from: 'getAccount', to: signInNode, kind: 'failure', label: '$statusCode == 401 — retry, ≤ 1×' })
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
    expect(graph.edges).toEqual([{ from: 'a', to: signInNode, kind: 'success', label: 'continue' }])
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
      [endNodeId('', 'showForbiddenPage'), 'showForbiddenPage'],
      [endNodeId('', 'showErrorPage'), 'showErrorPage'],
    ])
    expect(graph.edges).toContainEqual({ from: 'getAccount', to: endNodeId('', 'showErrorPage'), kind: 'failure', label: 'showErrorPage' })
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
      endNodeId('', 'showForbiddenPage'),
      endNodeId('', 'showErrorPage'),
    ])
    expect(graph.edges).toEqual([
      { from: 'getAccount', to: workflowNodeId('', 'signIn'), kind: 'failure', label: '$statusCode == 401 — retry, ≤ 1×' },
      { from: workflowNodeId('', 'signIn'), to: 'getAccount', kind: 'return' },
      { from: 'getAccount', to: endNodeId('', 'showForbiddenPage'), kind: 'failure', label: '$statusCode == 403' },
      { from: 'getAccount', to: endNodeId('', 'showErrorPage'), kind: 'failure', label: 'showErrorPage' },
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
})
