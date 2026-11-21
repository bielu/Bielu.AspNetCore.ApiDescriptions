// @vitest-environment happy-dom
import { flushPromises, mount, type VueWrapper } from '@vue/test-utils'
import { afterEach, beforeEach, describe, expect, it } from 'vitest'
import ArazzoWorkflow from '../src/components/ArazzoWorkflow.vue'
import { buildDataFlowGraph, dataWorkflowNodeId, INPUTS_NODE_ID, OUTPUTS_NODE_ID } from '../src/helpers/data-flow'
import { toWorkflowViews, traverseArazzoDocument } from '../src/helpers/navigation'
import { resetWorkflowView, VIEW_STORAGE_KEY } from '../src/helpers/view-mode'
import type { ArazzoDocument, ArazzoWorkflow } from '../src/types'

const placeOrder: ArazzoWorkflow = {
  workflowId: 'placeOrder',
  inputs: { type: 'object', properties: { sku: { type: 'string' }, quantity: { type: 'integer' } } },
  parameters: [{ name: 'X-Tenant', in: 'header', value: '$inputs.tenant' }],
  steps: [
    {
      stepId: 'create',
      requestBody: { payload: { sku: '$inputs.sku', quantity: '$inputs.quantity' } },
      outputs: { orderId: '$response.body#/id', total: '$response.body#/total' },
    },
    { stepId: 'announce', parameters: [{ name: 'id', value: '$steps.create.outputs.orderId' }] },
    {
      stepId: 'pay',
      requestBody: { payload: { order: '$steps.create.outputs.orderId', amount: '{$steps.create.outputs.total}' } },
      outputs: { paymentId: '$message.payload.id' },
    },
    { stepId: 'audit', parameters: [{ name: 'last', value: '$workflows.trackOrder.outputs.status' }] },
  ],
  outputs: { orderId: '$steps.create.outputs.orderId', paymentId: '$steps.pay.outputs.paymentId', again: '$workflows.placeOrder.outputs.orderId' },
}

const trackOrder: ArazzoWorkflow = { workflowId: 'trackOrder', steps: [{ stepId: 'status' }], outputs: { status: '$response.body#/status' } }

const document: ArazzoDocument = { arazzo: '1.1.0', workflows: [placeOrder, trackOrder] }

describe('buildDataFlowGraph', () => {
  it('draws inputs, steps, other workflows and outputs, with an arrow per producer and consumer', () => {
    // Act
    const graph = buildDataFlowGraph(document, placeOrder)

    // Assert
    expect(graph.nodes.map((node) => [node.id, node.kind])).toEqual([
      [INPUTS_NODE_ID, 'data'],
      [dataWorkflowNodeId('trackOrder'), 'workflow'],
      ['create', 'step'],
      ['announce', 'step'],
      ['pay', 'step'],
      ['audit', 'step'],
      [OUTPUTS_NODE_ID, 'data'],
    ])
    expect(graph.edges.map((edge) => `${edge.from} → ${edge.to}: ${edge.label}`)).toEqual([
      '::inputs → create: quantity, sku',
      '::workflow:trackOrder → audit: status',
      'create → announce: orderId',
      'create → pay: orderId, total',
      'create → ::outputs: orderId',
      'pay → ::outputs: paymentId',
    ])
  })

  it('leaves out workflow parameters, the workflow reading its own outputs, and values from the exchange', () => {
    // Act
    const graph = buildDataFlowGraph(document, placeOrder)

    // Assert: `$inputs.tenant` only reaches steps through the workflow's parameters.
    expect(graph.edges.some((edge) => edge.label?.includes('tenant'))).toBe(false)
    expect(graph.nodes.some((node) => node.id === dataWorkflowNodeId('placeOrder'))).toBe(false)
  })

  it('draws a step that passes nothing, and no inputs or outputs node without flows', () => {
    // Arrange
    const workflow: ArazzoWorkflow = { workflowId: 'w', steps: [{ stepId: 'only' }] }

    // Act
    const graph = buildDataFlowGraph({ arazzo: '1.1.0', workflows: [workflow] }, workflow)

    // Assert
    expect(graph.nodes.map((node) => node.id)).toEqual(['only'])
    expect(graph.edges).toEqual([])
  })

  it('marks steps with their spec, and draws a step that invokes a workflow as a workflow node', () => {
    // Arrange
    const workflow: ArazzoWorkflow = { workflowId: 'w', steps: [{ stepId: 'call', operationId: 'x' }, { stepId: 'nested', workflowId: 'other' }] }

    // Act
    const graph = buildDataFlowGraph({ arazzo: '1.1.0', workflows: [workflow] }, workflow, (step) => (step.workflowId ? 'arazzo' : 'openapi'))

    // Assert
    expect(graph.nodes).toEqual([
      { id: 'call', label: 'call', kind: 'step', stepId: 'call', spec: 'openapi' },
      { id: 'nested', label: 'nested', kind: 'workflow', workflowId: 'other', stepId: 'nested', spec: 'arazzo' },
    ])
  })

  it('is empty without steps', () => {
    // Act / Assert
    expect(buildDataFlowGraph(document, undefined).nodes).toEqual([])
    expect(buildDataFlowGraph(document, { workflowId: 'empty' }).nodes).toEqual([])
  })
})

describe('the Data flow view', () => {
  let wrapper: VueWrapper | undefined

  beforeEach(() => {
    localStorage.clear()
    resetWorkflowView()
  })

  afterEach(() => {
    wrapper?.unmount()
    wrapper = undefined
    localStorage.clear()
    resetWorkflowView()
  })

  const mountWorkflow = () => {
    const navigation = traverseArazzoDocument({ documentId: 'doc', document: document as unknown as Record<string, unknown> })
    const [view] = toWorkflowViews(document, navigation)
    return mount(ArazzoWorkflow, { props: { view: view!, document, documents: {} } })
  }

  it('switches the overview to the values passed, and is remembered', async () => {
    // Arrange
    wrapper = mountWorkflow()
    const dataFlow = wrapper.findAll('.ba-overview .ba-view-toggle button').find((button) => button.text() === 'Data flow')!

    // Act
    await dataFlow.trigger('click')
    await flushPromises()

    // Assert
    expect(dataFlow.attributes('aria-pressed')).toBe('true')
    expect(wrapper.find(`.ba-overview [data-node-id="${INPUTS_NODE_ID}"]`).classes()).toContain('ba-diagram-node-data')
    expect(wrapper.findAll('.ba-overview .ba-diagram-edge-label text').map((label) => label.text())).toContain('orderId, total')
    expect(wrapper.find('.ba-overview .ba-diagram-legend').text()).toContain('Values passed')
    expect(localStorage.getItem(VIEW_STORAGE_KEY)).toBe('data')

    // Act: a new page load reads the choice back.
    resetWorkflowView()
    wrapper.unmount()
    wrapper = mountWorkflow()
    await flushPromises()

    // Assert
    expect(wrapper.find(`.ba-overview [data-node-id="${OUTPUTS_NODE_ID}"]`).exists()).toBe(true)
  })
})
