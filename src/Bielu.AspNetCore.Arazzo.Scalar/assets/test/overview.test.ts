import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'
import { schemaPropertyRows } from '../src/helpers/format'
import { stepHeading } from '../src/helpers/heading'
import { INITIAL_PAN_ZOOM, MAX_SCALE, panBy, zoomAt } from '../src/helpers/pan-zoom'
import { resolveStepTarget, targetDescription, targetParameters } from '../src/helpers/resolve-target'
import {
  buildWorkflowGraph,
  FAILURE_NODE_ID,
  layoutWorkflowGraph,
  MAX_EDGE_LABEL_LENGTH,
  SUCCESS_NODE_ID,
} from '../src/helpers/workflow-graph'
import type { ArazzoDocument, ArazzoWorkflow } from '../src/types'

const documentWith = (workflow: ArazzoWorkflow, components?: ArazzoDocument['components']): ArazzoDocument => ({
  arazzo: '1.0.1',
  workflows: [workflow],
  components,
})

describe('buildWorkflowGraph', () => {
  it('chains steps in document order when none declares dependsOn', () => {
    // Arrange
    const workflow: ArazzoWorkflow = { workflowId: 'wf', steps: [{ stepId: 'a' }, { stepId: 'b' }, { stepId: 'c' }] }

    // Act
    const graph = buildWorkflowGraph(documentWith(workflow), workflow)

    // Assert
    expect(graph.nodes.map((node) => node.id)).toEqual(['a', 'b', 'c'])
    expect(graph.edges).toEqual([
      { from: 'a', to: 'b', kind: 'next' },
      { from: 'b', to: 'c', kind: 'next' },
    ])
  })

  it('follows dependsOn instead of document order when a step declares it', () => {
    // Arrange
    const workflow: ArazzoWorkflow = {
      workflowId: 'wf',
      steps: [{ stepId: 'a' }, { stepId: 'b' }, { stepId: 'c', dependsOn: ['a', 'missing'] }],
    }

    // Act
    const graph = buildWorkflowGraph(documentWith(workflow), workflow)

    // Assert
    expect(graph.edges).toEqual([{ from: 'a', to: 'c', kind: 'next', label: undefined }])
  })

  it('draws actions as labeled arrows, with end actions leading to Success/Failed', () => {
    // Arrange
    const workflow: ArazzoWorkflow = {
      workflowId: 'wf',
      steps: [
        {
          stepId: 'create',
          successCriteria: [{ condition: '$statusCode == 201' }],
          onSuccess: [{ name: 'done', type: 'end', criteria: [{ condition: '$response.body#/done == true' }] }],
          onFailure: [
            { name: 'again', type: 'retry', stepId: 'create', retryLimit: 3 },
            { reference: '$components.failureActions.giveUp' },
          ],
        },
        { stepId: 'confirm', dependsOn: ['create'] },
      ],
    }
    const document = documentWith(workflow, { failureActions: { giveUp: { name: 'giveUp', type: 'end' } } })

    // Act
    const graph = buildWorkflowGraph(document, workflow)

    // Assert
    expect(graph.nodes.map((node) => node.id)).toEqual(['create', 'confirm', SUCCESS_NODE_ID, FAILURE_NODE_ID])
    expect(graph.edges).toEqual([
      { from: 'create', to: 'confirm', kind: 'next', label: '$statusCode == 201' },
      { from: 'create', to: SUCCESS_NODE_ID, kind: 'success', label: '$response.body#/done == true' },
      { from: 'create', to: 'create', kind: 'failure', label: 'again — retry, ≤ 3×' },
      { from: 'create', to: FAILURE_NODE_ID, kind: 'failure', label: 'giveUp' },
    ])
  })

  it('does not repeat a dependsOn arrow an action already draws', () => {
    // Arrange
    const workflow: ArazzoWorkflow = {
      workflowId: 'wf',
      steps: [
        { stepId: 'a', onSuccess: [{ name: 'next', type: 'goto', stepId: 'b' }] },
        { stepId: 'b', dependsOn: ['a'] },
      ],
    }

    // Act
    const graph = buildWorkflowGraph(documentWith(workflow), workflow)

    // Assert
    expect(graph.edges).toEqual([{ from: 'a', to: 'b', kind: 'success', label: undefined }])
  })

  it('returns an empty graph for a workflow without steps', () => {
    // Arrange / Act / Assert
    expect(buildWorkflowGraph(documentWith({ workflowId: 'wf' }), undefined)).toEqual({ nodes: [], edges: [] })
  })
})

describe('layoutWorkflowGraph', () => {
  it('places the steps top-down and routes every arrow', () => {
    // Arrange
    const workflow: ArazzoWorkflow = {
      workflowId: 'wf',
      steps: [
        { stepId: 'first', onFailure: [{ name: 'stop', type: 'end', criteria: [{ condition: 'x'.repeat(80) }] }] },
        { stepId: 'second' },
      ],
    }

    // Act
    const layout = layoutWorkflowGraph(buildWorkflowGraph(documentWith(workflow), workflow))

    // Assert
    const [first, second] = layout.nodes
    expect(first!.y).toBeLessThan(second!.y)
    expect(layout.width).toBeGreaterThan(0)
    expect(layout.height).toBeGreaterThan(second!.y)
    expect(layout.edges.every((edge) => edge.points.length >= 2)).toBe(true)
    const labeled = layout.edges.find((edge) => edge.label)!
    expect(labeled.label!.length).toBe(MAX_EDGE_LABEL_LENGTH)
    expect(labeled.label!.endsWith('…')).toBe(true)
    expect(labeled.labelPosition).toBeDefined()
  })
})

describe('stepHeading', () => {
  it.each([
    [{ stepId: 'create', description: 'Create the order.' }, { title: 'Create the order.' }],
    [{ stepId: 'create' }, { title: 'create', description: undefined }],
    [{ stepId: 'create', description: 'Line one\nLine two' }, { title: 'create', description: 'Line one\nLine two' }],
    [{ stepId: 'create', description: 'x'.repeat(101) }, { title: 'create', description: 'x'.repeat(101) }],
  ])('uses a short one-line description as the title: %j', (step, expected) => {
    // Arrange / Act / Assert
    expect(stepHeading(step)).toEqual(expected)
  })
})

describe('schemaPropertyRows nesting', () => {
  it('nests object and array-of-object properties and lists their details', () => {
    // Arrange
    const schema = {
      type: 'object',
      properties: {
        address: { $ref: '#/$defs/address' },
        lines: { type: 'array', items: { $ref: '#/$defs/line' } },
        status: { type: 'string', enum: ['new', 'paid'], default: 'new' },
      },
      $defs: {
        address: { type: 'object', required: ['city'], properties: { city: { type: 'string', minLength: 1 } } },
        line: { type: 'object', properties: { sku: { type: 'string', format: 'uuid' } } },
      },
    }

    // Act
    const rows = schemaPropertyRows(schema)

    // Assert
    expect(rows).toEqual([
      {
        name: 'address',
        type: 'object',
        required: false,
        description: '',
        children: [{ name: 'city', type: 'string', required: true, description: '', details: ['min length: 1'] }],
      },
      {
        name: 'lines',
        type: 'array<object>',
        required: false,
        description: '',
        children: [{ name: 'sku', type: 'string', required: false, description: '', details: ['uuid'] }],
      },
      { name: 'status', type: 'string', required: false, description: '', details: ['one of: "new", "paid"', 'default: "new"'] },
    ])
  })

  it('stops expanding a recursive reference', () => {
    // Arrange
    const schema = {
      type: 'object',
      properties: { node: { $ref: '#/$defs/node' } },
      $defs: { node: { type: 'object', properties: { next: { $ref: '#/$defs/node' } } } },
    }

    // Act
    const rows = schemaPropertyRows(schema)

    // Assert
    expect(rows![0]!.children![0]).toEqual({ name: 'next', type: 'node', required: false, description: '' })
  })
})

describe('targetParameters', () => {
  const api = {
    openapi: '3.1.0',
    'x-scalar-original-source-url': 'https://app.test/openapi.json',
    paths: {
      '/orders/{id}': {
        parameters: [{ $ref: '#/components/parameters/id' }, { name: 'trace', in: 'header' }],
        get: {
          operationId: 'getOrder',
          summary: 'Get an order',
          description: 'Returns the order.',
          parameters: [{ name: 'trace', in: 'header', required: true, description: 'Trace id' }],
        },
      },
    },
    components: { parameters: { id: { name: 'id', in: 'path', schema: { type: 'string', format: 'uuid' } } } },
  }
  const events = {
    asyncapi: '3.0.0',
    'x-scalar-original-source-url': 'https://app.test/asyncapi.json',
    channels: { order: { address: 'orders.{id}', description: 'Order events', parameters: { id: { description: 'Order id' } } } },
    operations: { onOrder: { action: 'receive', channel: { $ref: '#/channels/order' } } },
  }
  const arazzo: ArazzoDocument = {
    arazzo: '1.0.1',
    sourceDescriptions: [
      { name: 'api', url: 'https://app.test/openapi.json' },
      { name: 'events', url: 'https://app.test/asyncapi.json', type: 'asyncapi' },
    ],
  }
  const documents = { api, events }

  beforeEach(() => {
    vi.stubGlobal('location', { href: 'https://app.test/scalar/' })
  })

  afterEach(() => {
    vi.unstubAllGlobals()
  })

  it('merges path item and operation parameters, the operation winning', () => {
    // Arrange
    const target = resolveStepTarget(arazzo, { stepId: 's', operationId: 'getOrder' }, documents)

    // Act
    const parameters = targetParameters(target, documents)

    // Assert
    expect(parameters).toEqual([
      { name: 'id', location: 'path', required: true, type: 'string (uuid)', description: '' },
      { name: 'trace', location: 'header', required: true, type: 'string', description: 'Trace id' },
    ])
    expect(targetDescription(target)).toBe('Returns the order.')
  })

  it('lists a channel target’s address parameters', () => {
    // Arrange
    const target = resolveStepTarget(arazzo, { stepId: 's', operationId: 'onOrder' }, documents)

    // Act
    const parameters = targetParameters(target, documents)

    // Assert
    expect(parameters).toEqual([{ name: 'id', location: 'channel', required: true, type: 'string', description: 'Order id' }])
    expect(targetDescription(target)).toBe('Order events')
  })

  it('returns nothing for an unresolved or workflow target', () => {
    // Arrange
    const unresolved = resolveStepTarget(arazzo, { stepId: 's', operationId: 'nope' }, documents)

    // Act / Assert
    expect(targetParameters(unresolved, documents)).toEqual([])
    expect(targetParameters({ kind: 'workflow', identifier: 'wf', workflow: undefined }, documents)).toEqual([])
    expect(targetDescription(undefined)).toBe('')
  })
})

describe('pan and zoom', () => {
  it('zooms around the pivot and stops at the limits', () => {
    // Arrange
    const pivot = { x: 100, y: 50 }

    // Act
    const zoomed = zoomAt(INITIAL_PAN_ZOOM, 'in', pivot)
    let maxed = INITIAL_PAN_ZOOM
    for (let index = 0; index < 50; index++) {
      maxed = zoomAt(maxed, 'in', pivot)
    }

    // Assert
    expect(zoomed.scale).toBeCloseTo(1.1)
    // The pivot stays put: (pivot - offset) / scale is the same point of the canvas before and after.
    expect((pivot.x - zoomed.x) / zoomed.scale).toBeCloseTo(pivot.x)
    expect(maxed.scale).toBe(MAX_SCALE)
    expect(zoomAt(maxed, 'in', pivot)).toBe(maxed)
  })

  it('pans by the pointer movement', () => {
    // Arrange / Act / Assert
    expect(panBy(INITIAL_PAN_ZOOM, 5, -3)).toEqual({ x: 5, y: -3, scale: 1 })
  })
})
