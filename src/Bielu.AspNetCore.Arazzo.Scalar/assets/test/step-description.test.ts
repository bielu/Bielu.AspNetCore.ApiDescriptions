// @vitest-environment happy-dom
import { mount } from '@vue/test-utils'
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'
import ArazzoWorkflowDiagram from '../src/components/ArazzoWorkflowDiagram.vue'
import { describeStep } from '../src/helpers/step-description'
import { buildWorkflowGraph, layoutWorkflowGraph } from '../src/helpers/workflow-graph'
import type { ArazzoDocument, ArazzoStep, ArazzoWorkflow } from '../src/types'

const ordersApi = {
  openapi: '3.1.0',
  'x-scalar-original-source-url': '/openapi.json',
  paths: { '/orders': { post: { operationId: 'createOrder' } } },
}

const eventsApi = {
  asyncapi: '3.0.0',
  'x-scalar-original-source-url': '/asyncapi.json',
  channels: { orderPlaced: { address: 'orders.placed' }, paymentCompleted: { address: 'payments.completed' } },
  operations: {
    publishOrderPlaced: { action: 'send', channel: { $ref: '#/channels/orderPlaced' } },
    onPaymentCompleted: { action: 'receive', channel: { $ref: '#/channels/paymentCompleted' } },
  },
}

const documents = { orders: ordersApi, events: eventsApi }

const arazzo: ArazzoDocument = {
  arazzo: '1.1.0',
  sourceDescriptions: [
    { name: 'orders', url: '/openapi.json', type: 'openapi' },
    { name: 'events', url: '/asyncapi.json', type: 'asyncapi' },
  ],
  workflows: [],
}

const describe_ = (step: Omit<ArazzoStep, 'stepId'>, loaded: Record<string, unknown> = documents) =>
  describeStep({ stepId: 's', ...step }, arazzo, loaded)

beforeEach(() => {
  vi.stubGlobal('location', { href: 'https://app.test/scalar/' })
})

afterEach(() => {
  vi.unstubAllGlobals()
})

describe('describeStep', () => {
  it('names the HTTP method and path of an OpenAPI step', () => {
    // Act / Assert
    expect(describe_({ operationId: 'createOrder' })).toEqual({ detail: 'POST /orders', explanation: 'Calls POST /orders' })
  })

  it('says an AsyncAPI step publishes on its channel', () => {
    // Act / Assert
    expect(describe_({ operationId: 'publishOrderPlaced' })).toEqual({
      detail: '→ sends orderPlaced',
      explanation: 'Publishes a message on orderPlaced (orders.placed)',
    })
  })

  it('says what a receiving step waits for: the channel, the correlation and the timeout', () => {
    // Act
    const description = describe_({
      channelPath: '{$sourceDescriptions.events.url}#/channels/paymentCompleted',
      action: 'receive',
      correlationId: '$steps.create.outputs.orderId',
      timeout: 15000,
    })

    // Assert
    expect(description).toEqual({
      detail: '← receives paymentCompleted · 15 s',
      explanation: 'Waits for a message on paymentCompleted (payments.completed), the one matching orderId from create; fails after 15 s',
    })
  })

  it("takes the channel from the step's path when its document is not loaded", () => {
    // Act
    const description = describe_({ channelPath: '{$sourceDescriptions.events.url}#/channels/orderPlaced', action: 'send' }, {})

    // Assert
    expect(description?.detail).toBe('→ sends orderPlaced')
  })

  it('describes nothing for a workflow step or an unresolved operation', () => {
    // Act / Assert
    expect(describe_({ workflowId: 'other' })).toBeUndefined()
    expect(describe_({ operationId: 'missing' })).toBeUndefined()
  })
})

describe('the overview', () => {
  it('draws the description under the step label, and says it in the tooltip', () => {
    // Arrange
    const workflow: ArazzoWorkflow = {
      workflowId: 'shop',
      steps: [{ stepId: 'wait', channelPath: '{$sourceDescriptions.events.url}#/channels/paymentCompleted', action: 'receive', timeout: 60000 }],
    }
    const layout = layoutWorkflowGraph(
      buildWorkflowGraph({ ...arazzo, workflows: [workflow] }, workflow, undefined, {
        describeStep: (step, owner) => describeStep(step, owner, documents),
      }),
    )

    // Act
    const view = mount(ArazzoWorkflowDiagram, { props: { layout, stepSectionIds: {} } })

    // Assert
    const box = view.find('[data-node-id="wait"]')
    expect(box.find('.ba-diagram-subtitle').text()).toBe('← receives paymentCompleted · 1 min')
    expect(box.find('title').text()).toContain('Waits for a message on paymentCompleted (payments.completed); fails after 1 min')
    const node = layout.nodes.find((candidate) => candidate.id === 'wait')!
    expect(node.width).toBeGreaterThan('← receives paymentCompleted · 1 min'.length * 0.9 * 7)
    view.unmount()
  })
})
