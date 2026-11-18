import { describe, expect, it } from 'vitest'
import { nestedWorkflowSteps } from '../src/helpers/nested-workflow'
import { resolveStepTarget, resolveWorkflow } from '../src/helpers/resolve-target'
import { buildWorkflowGraph, FAILURE_NODE_ID, groupId, layoutWorkflowGraph, workflowNodeId } from '../src/helpers/workflow-graph'
import type { ArazzoDocument, ArazzoWorkflow } from '../src/types'

/**
 * Orders calls Identity's `signIn`, which Identity publishes in its own Arazzo document. Orders also has a
 * local `signIn` of its own, so a qualified call must never land there. Identity's `signIn` can send the
 * customer back to Orders' `placeOrder`, which closes a loop across the two documents.
 */
const orders = {
  arazzo: '1.1.0',
  'x-scalar-original-source-url': 'https://shop.test/orders.arazzo.json',
  'x-pages': { login: { title: 'Orders login' } },
  sourceDescriptions: [{ name: 'identity', url: '/identity.arazzo.json', type: 'arazzo' }],
  workflows: [
    {
      workflowId: 'placeOrder',
      steps: [
        { stepId: 'authenticate', workflowId: '$sourceDescriptions.identity.signIn' },
        { stepId: 'create', operationId: 'createOrder' },
      ],
    },
    { workflowId: 'signIn', steps: [{ stepId: 'legacyLogin', operationId: 'legacyLogin' }] },
  ],
} as unknown as ArazzoDocument

const identity = {
  arazzo: '1.1.0',
  'x-scalar-original-source-url': 'https://shop.test/identity.arazzo.json',
  'x-pages': { login: { title: 'Identity sign-in', route: '/sign-in' } },
  sourceDescriptions: [{ name: 'orders', url: '/orders.arazzo.json', type: 'arazzo' }],
  components: { failureActions: { locked: { name: 'locked', type: 'end' } } },
  workflows: [
    {
      workflowId: 'signIn',
      summary: 'Sign the customer in',
      steps: [
        { stepId: 'authorize', operationId: 'authorize', 'x-page': 'login', onFailure: [{ reference: '$components.failureActions.locked' }] },
        { stepId: 'resume', workflowId: '$sourceDescriptions.orders.placeOrder' },
      ],
    },
  ],
} as unknown as ArazzoDocument

const documents = { orders, identity }
const placeOrder = orders.workflows![0] as ArazzoWorkflow
const identityNode = 'authenticate'

const graphOf = (options: { expanded?: string[]; documents?: Record<string, unknown>; view?: 'pages' } = {}) =>
  buildWorkflowGraph(orders, placeOrder, undefined, {
    documents: options.documents ?? documents,
    documentName: 'orders',
    expanded: new Set(options.expanded ?? []),
    ...(options.view ? { view: options.view, pages: { pagesExtension: 'x-pages', pageExtension: 'x-page' } } : {}),
  })

describe('resolveWorkflow', () => {
  it('finds a qualified workflow in the loaded Arazzo document its source names, not a local one of the same id', () => {
    // Act
    const resolved = resolveWorkflow(orders, '$sourceDescriptions.identity.signIn', documents)

    // Assert
    expect(resolved?.documentName).toBe('identity')
    expect(resolved?.document).toMatchObject({ 'x-scalar-original-source-url': 'https://shop.test/identity.arazzo.json' })
    expect(resolved?.workflow.steps!.map((step) => step.stepId)).toEqual(['authorize', 'resume'])
    expect(resolveWorkflow(orders, 'signIn', documents)?.workflow.steps!.map((step) => step.stepId)).toEqual(['legacyLogin'])
  })

  it('finds nothing when the document is not loaded, the source is unknown or of another type', () => {
    // Act / Assert
    expect(resolveWorkflow(orders, '$sourceDescriptions.identity.signIn', {})).toBeUndefined()
    expect(resolveWorkflow(orders, '$sourceDescriptions.billing.pay', documents)).toBeUndefined()
    const typed = { ...orders, sourceDescriptions: [{ name: 'identity', url: '/identity.arazzo.json', type: 'openapi' }] } as ArazzoDocument
    expect(resolveWorkflow(typed, '$sourceDescriptions.identity.signIn', documents)).toBeUndefined()
  })

  it('resolves the step target and the inline step list in the other document', () => {
    // Arrange
    const step = placeOrder.steps![0]!

    // Act
    const target = resolveStepTarget(orders, step, documents)
    const nested = nestedWorkflowSteps(orders, step, documents)

    // Assert
    expect(target).toMatchObject({ kind: 'workflow', documentName: 'identity', workflow: { summary: 'Sign the customer in' } })
    expect(nested?.steps.map((entry) => entry.stepId)).toEqual(['authorize', 'resume'])
  })
})

describe('expanding a workflow of another Arazzo document', () => {
  it('draws the other document\'s steps, resolving their component actions there', () => {
    // Act
    const graph = graphOf({ expanded: [identityNode] })

    // Assert
    const node = graph.nodes.find((candidate) => candidate.id === identityNode)!
    expect(node).toMatchObject({ kind: 'workflow', expandable: true, expanded: true })
    expect(graph.groups).toEqual([
      { id: groupId(identityNode), label: 'identity.signIn', workflowId: '$sourceDescriptions.identity.signIn', documentName: 'identity' },
    ])
    expect(graph.nodes.filter((candidate) => candidate.group === groupId(identityNode)).map((candidate) => candidate.id)).toContain(`${identityNode}/authorize`)
    expect(graph.edges).toContainEqual(expect.objectContaining({ from: `${identityNode}/authorize`, to: `${identityNode}/${FAILURE_NODE_ID}`, action: 'locked' }))
  })

  it('marks a call back into the drawn document as a cycle, keyed by document and workflow', () => {
    // Act
    const graph = graphOf({ expanded: [identityNode] })

    // Assert
    expect(graph.nodes.find((candidate) => candidate.id === `${identityNode}/resume`)).toMatchObject({ cycle: true, expandable: false })
  })

  it('names pages after the registry of the document each step belongs to', () => {
    // Act
    const graph = graphOf({ expanded: [identityNode], view: 'pages' })

    // Assert
    const band = graph.bands.find((candidate) => candidate.nodes.includes(`${identityNode}/authorize`))
    expect(band).toMatchObject({ pageId: 'login', title: 'Identity sign-in', route: '/sign-in' })
  })

  it('keeps a readable qualified identifier when the other document is not loaded', () => {
    // Act
    const graph = graphOf({ documents: { orders } })
    const goto = buildWorkflowGraph(
      orders,
      { workflowId: 'w', steps: [{ stepId: 'a', onFailure: [{ name: 'login', type: 'goto', workflowId: '$sourceDescriptions.identity.signIn' }] }] },
      undefined,
      { documents: { orders }, documentName: 'orders' },
    )

    // Assert
    expect(graph.nodes.find((candidate) => candidate.id === identityNode)).toMatchObject({ expandable: false, label: identityNode })
    expect(goto.nodes.find((candidate) => candidate.id === workflowNodeId('', '$sourceDescriptions.identity.signIn'))).toMatchObject({
      label: 'identity.signIn',
      expandable: false,
    })
  })

  it('lays the other document\'s steps out inside their group', () => {
    // Act
    const layout = layoutWorkflowGraph(graphOf({ expanded: [identityNode] }))

    // Assert
    const group = layout.groups.find((candidate) => candidate.id === groupId(identityNode))!
    const authorize = layout.nodes.find((candidate) => candidate.id === `${identityNode}/authorize`)!
    expect(authorize.x - authorize.width / 2).toBeGreaterThanOrEqual(group.x - group.width / 2)
    expect(authorize.x + authorize.width / 2).toBeLessThanOrEqual(group.x + group.width / 2)
  })
})
