// @vitest-environment happy-dom
import { flushPromises, mount, type VueWrapper } from '@vue/test-utils'
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'
import ArazzoSequenceDiagram from '../src/components/ArazzoSequenceDiagram.vue'
import ArazzoWorkflow from '../src/components/ArazzoWorkflow.vue'
import { toWorkflowViews, traverseArazzoDocument } from '../src/helpers/navigation'
import { buildSequence, CLIENT_ID, runOrder } from '../src/helpers/sequence'
import { resetWorkflowView } from '../src/helpers/view-mode'
import type { ArazzoDocument, ArazzoStep, ArazzoWorkflow as Workflow } from '../src/types'

const ordersApi = {
  openapi: '3.1.0',
  info: { title: 'Shop' },
  'x-scalar-original-source-url': '/openapi.json',
  paths: { '/orders': { post: { operationId: 'createOrder' } } },
}

const eventsApi = {
  asyncapi: '3.0.0',
  info: { title: 'Shop' },
  'x-scalar-original-source-url': '/asyncapi.json',
  channels: { orderPlaced: { address: 'orders.placed' }, paymentCompleted: { address: 'payments.completed' } },
  operations: { publishOrderPlaced: { action: 'send', channel: { $ref: '#/channels/orderPlaced' } } },
}

const documents = { orders: ordersApi, events: eventsApi }

const placeOrder: Workflow = {
  workflowId: 'placeOrder',
  steps: [
    {
      stepId: 'create',
      operationId: 'createOrder',
      successCriteria: [{ condition: '$statusCode == 201' }],
      onFailure: [{ name: 'rejected', type: 'end', criteria: [{ condition: '$statusCode == 400' }] }],
    },
    { stepId: 'announce', operationId: 'publishOrderPlaced' },
    {
      stepId: 'paid',
      channelPath: '{$sourceDescriptions.events.url}#/channels/paymentCompleted',
      action: 'receive',
      timeout: 60000,
      correlationId: '$steps.create.outputs.orderId',
    },
    { stepId: 'track', workflowId: 'trackOrder' },
  ],
}

const document: ArazzoDocument = {
  arazzo: '1.1.0',
  sourceDescriptions: [
    { name: 'orders', url: '/openapi.json', type: 'openapi' },
    { name: 'events', url: '/asyncapi.json', type: 'asyncapi' },
  ],
  workflows: [placeOrder, { workflowId: 'trackOrder', steps: [{ stepId: 'status' }] }],
}

beforeEach(() => {
  vi.stubGlobal('location', { href: 'https://app.test/scalar/' })
})

afterEach(() => {
  vi.unstubAllGlobals()
})

describe('runOrder', () => {
  it('keeps document order without dependsOn, and follows it with', () => {
    // Arrange
    const steps: ArazzoStep[] = [{ stepId: 'c', dependsOn: ['b'] }, { stepId: 'a' }, { stepId: 'b', dependsOn: ['a'] }]

    // Act / Assert
    expect(runOrder([{ stepId: 'x' }, { stepId: 'y' }]).map((step) => step.stepId)).toEqual(['x', 'y'])
    expect(runOrder(steps).map((step) => step.stepId)).toEqual(['a', 'b', 'c'])
  })

  it('does not stop on a dependsOn cycle', () => {
    // Act / Assert
    expect(runOrder([{ stepId: 'a', dependsOn: ['b'] }, { stepId: 'b', dependsOn: ['a'] }]).map((step) => step.stepId)).toEqual(['a', 'b'])
  })

  it('runs a step after the step whose output it reads, as an implicit dependency', () => {
    // Arrange
    const steps: ArazzoStep[] = [
      { stepId: 'start' },
      { stepId: 'use', dependsOn: ['start'], parameters: [{ name: 'id', in: 'path', value: '$steps.produce.outputs.id' }] },
      { stepId: 'produce', dependsOn: ['start'], outputs: { id: '$response.body#/id' } },
    ]

    // Act / Assert
    expect(runOrder(steps).map((step) => step.stepId)).toEqual(['start', 'produce', 'use'])
  })

  it('draws the sequence in that order', () => {
    // Arrange
    const workflow: Workflow = {
      workflowId: 'w',
      steps: [
        { stepId: 'start', operationId: 'a' },
        { stepId: 'use', operationId: 'b', dependsOn: ['start'], requestBody: { payload: { id: '$steps.produce.outputs.id' } } },
        { stepId: 'produce', operationId: 'c', dependsOn: ['start'] },
      ],
    }

    // Act
    const sequence = buildSequence({ arazzo: '1.1.0', workflows: [workflow] }, workflow, {})

    // Assert
    expect([...new Set(sequence.messages.map((message) => message.stepId))]).toEqual(['start', 'produce', 'use'])
  })
})

describe('buildSequence', () => {
  it('draws a lane per source, named after it, and the messages in run order', () => {
    // Act
    const sequence = buildSequence(document, placeOrder, documents)

    // Assert
    expect(sequence.participants).toEqual([
      { id: CLIENT_ID, label: 'Client' },
      { id: 'source:orders', label: 'orders', spec: 'openapi', detail: 'OpenAPI · Shop' },
      { id: 'source:events', label: 'events', spec: 'asyncapi', detail: 'AsyncAPI · Shop' },
      { id: 'workflow:trackOrder', label: 'workflow trackOrder', spec: 'arazzo', detail: 'Arazzo' },
    ])
    expect(sequence.messages).toEqual([
      { stepId: 'create', from: CLIENT_ID, to: 'source:orders', label: 'POST /orders', kind: 'request' },
      { stepId: 'create', from: 'source:orders', to: CLIENT_ID, label: '201', kind: 'response', note: 'on 400: rejected (end)' },
      { stepId: 'announce', from: CLIENT_ID, to: 'source:events', label: 'orderPlaced', kind: 'send' },
      { stepId: 'paid', from: 'source:events', to: CLIENT_ID, label: 'paymentCompleted', kind: 'receive', note: 'waits up to 1 min, matched on orderId from create' },
      { stepId: 'track', from: CLIENT_ID, to: 'workflow:trackOrder', label: 'runs the workflow', kind: 'invoke' },
    ])
  })

  it('labels the response "response" when the criteria name no status, and notes at most three failure paths', () => {
    // Arrange
    const workflow: Workflow = {
      workflowId: 'w',
      steps: [
        {
          stepId: 'create',
          operationId: 'createOrder',
          onFailure: [
            { name: 'a', type: 'end', criteria: [{ condition: '$statusCode == 400' }] },
            { name: 'b', type: 'goto', stepId: 'create', criteria: [{ condition: '$response.body#/retry == true' }] },
            { name: 'c', type: 'goto', workflowId: 'other', criteria: [{ condition: '$statusCode == 409' }] },
            { name: 'd', type: 'end' },
          ],
        },
      ],
    }

    // Act
    const [, response] = buildSequence(document, workflow, documents).messages

    // Assert
    expect(response).toMatchObject({ label: 'response', note: 'on 400: a (end); if: b (→ create); on 409: c (→ other); …' })
  })

  it('still draws steps whose documents are not loaded, under their source', () => {
    // Act
    const sequence = buildSequence(document, placeOrder, {})

    // Assert
    expect(sequence.participants.map((participant) => participant.label)).toContain('events')
    expect(sequence.messages.find((message) => message.stepId === 'paid')).toMatchObject({ from: 'source:events', kind: 'receive' })
  })

  it('has only the client for a workflow without steps', () => {
    // Act / Assert
    expect(buildSequence(document, { workflowId: 'empty' }, documents)).toEqual({ participants: [{ id: CLIENT_ID, label: 'Client' }], messages: [] })
  })
})

describe('the sequence diagram', () => {
  let wrapper: VueWrapper | undefined

  afterEach(() => {
    wrapper?.unmount()
    wrapper = undefined
    localStorage.clear()
    resetWorkflowView()
  })

  it('draws lanes and messages, names each step once, and opens a step from its name', async () => {
    // Arrange
    const host = globalThis.document.createElement('div')
    host.innerHTML = '<section id="doc/step/create"></section>'
    globalThis.document.body.appendChild(host)
    const section = host.querySelector('section')!
    section.scrollIntoView = vi.fn()
    wrapper = mount(ArazzoSequenceDiagram, {
      attachTo: globalThis.document.body,
      props: { sequence: buildSequence(document, placeOrder, documents), stepSectionIds: { create: 'doc/step/create' } },
    })

    // Act
    await wrapper.find('.ba-sequence-step').trigger('click')
    await flushPromises()

    // Assert
    expect(wrapper.findAll('.ba-sequence-participant')).toHaveLength(4)
    expect(wrapper.findAll('.ba-sequence-message')).toHaveLength(5)
    expect(wrapper.findAll('.ba-sequence-step text').map((step) => step.text())).toEqual(['create', 'announce', 'paid', 'track'])
    expect(wrapper.find('.ba-sequence-response .ba-sequence-line').classes()).toContain('ba-sequence-dashed')
    expect(wrapper.find('.ba-sequence-receive .ba-sequence-note').text()).toBe('waits up to 1 min, matched on orderId from create')
    expect(section.scrollIntoView).toHaveBeenCalled()
    host.remove()
  })

  it('replaces the overview when Sequence is chosen, without export or legend', async () => {
    // Arrange
    localStorage.clear()
    resetWorkflowView()
    const [view] = toWorkflowViews(document, traverseArazzoDocument({ documentId: 'doc', document: document as unknown as Record<string, unknown> }))
    wrapper = mount(ArazzoWorkflow, { props: { view: view!, document, documents } })

    // Act
    await wrapper.findAll('.ba-overview .ba-view-toggle button').find((button) => button.text() === 'Sequence')!.trigger('click')
    await flushPromises()

    // Assert
    expect(wrapper.find('.ba-overview .ba-sequence').exists()).toBe(true)
    expect(wrapper.find('.ba-overview .ba-diagram').exists()).toBe(false)
    expect(wrapper.find('.ba-overview .ba-export').exists()).toBe(false)
    expect(wrapper.find('.ba-overview .ba-diagram-legend').exists()).toBe(false)
  })
})
