// @vitest-environment happy-dom
import { flushPromises, mount, type VueWrapper } from '@vue/test-utils'
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'
import ArazzoDataFlowDetails from '../src/components/ArazzoDataFlowDetails.vue'
import ArazzoWorkflow from '../src/components/ArazzoWorkflow.vue'
import { INPUTS_NODE_ID, OUTPUTS_NODE_ID } from '../src/helpers/data-flow'
import { describeFlow } from '../src/helpers/data-flow-values'
import { toWorkflowViews, traverseArazzoDocument } from '../src/helpers/navigation'
import { resetWorkflowView, setWorkflowView } from '../src/helpers/view-mode'
import type { ArazzoDocument, ArazzoWorkflow as Workflow } from '../src/types'

const ordersApi = {
  openapi: '3.1.0',
  'x-scalar-original-source-url': '/openapi.json',
  // Scalar's sidebar tree: the operation, and the named schemas under Models.
  'x-scalar-navigation': {
    children: [
      { type: 'operation', id: 'orders/tag/orders/POST/orders', method: 'post', path: '/orders' },
      { type: 'models', id: 'orders/models', children: [{ type: 'model', id: 'orders/model/money', ref: '#/components/schemas/money', name: 'money' }] },
    ],
  },
  paths: {
    '/orders': {
      post: {
        operationId: 'createOrder',
        responses: {
          '201': {
            description: 'Created',
            content: { 'application/json': { schema: { $ref: '#/components/schemas/order' } } },
          },
          '400': { description: 'Rejected' },
        },
      },
    },
  },
  components: {
    schemas: {
      order: {
        type: 'object',
        properties: {
          id: { type: 'string', format: 'uuid', description: 'The order id.' },
          lines: { type: 'array', items: { type: 'object', properties: { sku: { type: 'string', maxLength: 12 } } } },
          total: { $ref: '#/components/schemas/money' },
        },
      },
      money: { type: 'object', properties: { amount: { type: 'number' }, currency: { type: 'string' } } },
    },
  },
}

const eventsApi = {
  asyncapi: '3.0.0',
  'x-scalar-original-source-url': '/asyncapi.json',
  channels: {
    paid: { address: 'paid', messages: { paymentCompleted: { $ref: '#/components/messages/paymentCompleted' } } },
  },
  components: {
    messages: {
      paymentCompleted: { title: 'Payment completed', payload: { type: 'object', properties: { paymentId: { type: 'string', format: 'uuid' } } } },
    },
  },
}

const documents = { orders: ordersApi, events: eventsApi }

const placeOrder: Workflow = {
  workflowId: 'placeOrder',
  inputs: { $ref: '#/components/inputs/order' },
  steps: [
    {
      stepId: 'create',
      operationId: '$sourceDescriptions.orders.createOrder',
      successCriteria: [{ condition: '$statusCode == 201' }],
      requestBody: { payload: { sku: '$inputs.sku' } },
      outputs: { orderId: '$response.body#/id', total: '$response.body#/total', firstSku: '$response.body#/lines/0/sku', location: '$response.header.Location' },
    },
    {
      stepId: 'paid',
      channelPath: '{$sourceDescriptions.events.url}#/channels/paid',
      action: 'receive',
      correlationId: '$steps.create.outputs.orderId',
      parameters: [{ name: 'order', in: 'query', value: '$steps.create.outputs.orderId' }],
      outputs: { paymentId: '$message.payload#/paymentId' },
    },
  ],
  outputs: { total: '$steps.create.outputs.total', paymentId: '$steps.paid.outputs.paymentId', where: '$steps.create.outputs.location' },
}

const document: ArazzoDocument = {
  arazzo: '1.1.0',
  sourceDescriptions: [
    { name: 'orders', url: '/openapi.json', type: 'openapi' },
    { name: 'events', url: '/asyncapi.json', type: 'asyncapi' },
  ],
  workflows: [placeOrder],
  components: { inputs: { order: { type: 'object', properties: { sku: { type: 'string', pattern: '^[A-Z]+$' } } } } },
}

beforeEach(() => {
  vi.stubGlobal('location', { href: 'https://app.test/scalar/' })
})

afterEach(() => {
  vi.unstubAllGlobals()
})

describe('describeFlow', () => {
  it('describes a step output by the response body schema at its pointer, references followed', () => {
    // Act
    const [orderId, total] = describeFlow(document, placeOrder, 'create', 'paid', ['orderId', 'total'], documents)

    // Assert
    expect(orderId).toMatchObject({
      name: 'orderId',
      source: '$response.body#/id',
      origin: '201 response body of POST /orders',
      schema: { name: 'orderId', type: 'string', details: ['uuid'], description: 'The order id.' },
      uses: ['query parameter order', 'correlation'],
    })
    // Described where it is made, though this consumer doesn't read it; the referenced schema's properties fold out.
    expect(total).toMatchObject({ uses: [], schema: { type: 'object' } })
    expect(total!.schema!.children!.map((row) => row.name)).toEqual(['amount', 'currency'])
  })

  it('walks array items in a pointer, and narrows a value by the pointer its reader adds', () => {
    // Arrange
    const reader: Workflow = { ...placeOrder, outputs: { amount: '$steps.create.outputs.total#/amount' } }

    // Act
    const [firstSku] = describeFlow(document, placeOrder, 'create', OUTPUTS_NODE_ID, ['firstSku'], documents)
    const [total] = describeFlow({ ...document, workflows: [reader] }, reader, 'create', OUTPUTS_NODE_ID, ['total'], documents)

    // Assert
    expect(firstSku!.schema).toMatchObject({ type: 'string', details: ['max length: 12'] })
    expect(total).toMatchObject({ source: '$response.body#/total', uses: ['workflow output amount'], schema: { name: 'total', type: 'number' } })
  })

  it('describes an input by the inputs schema, a message payload by the message, and a header by name only', () => {
    // Act
    const [sku] = describeFlow(document, placeOrder, INPUTS_NODE_ID, 'create', ['sku'], documents)
    const [paymentId] = describeFlow(document, placeOrder, 'paid', OUTPUTS_NODE_ID, ['paymentId'], documents)
    const [location] = describeFlow(document, placeOrder, 'create', OUTPUTS_NODE_ID, ['location'], documents)

    // Assert
    expect(sku).toMatchObject({ source: '$inputs.sku', origin: "the workflow's inputs", uses: ['request body'], schema: { type: 'string', details: ['pattern: ^[A-Z]+$'] } })
    expect(paymentId).toMatchObject({ source: '$message.payload#/paymentId', origin: 'payload of message Payment completed', schema: { type: 'string', details: ['uuid'] } })
    expect(location).toMatchObject({ source: '$response.header.Location', origin: 'response header Location', uses: ['workflow output where'] })
    expect(location!.schema).toBeUndefined()
  })

  it('links a value to its named schema in Scalar, else to the operation that makes it, or to the input row', () => {
    // Act
    const [orderId, total] = describeFlow(document, placeOrder, 'create', OUTPUTS_NODE_ID, ['orderId', 'total'], documents)
    const [sku] = describeFlow(document, placeOrder, INPUTS_NODE_ID, 'create', ['sku'], documents, 'doc/workflow/placeOrder')
    const [unlinked] = describeFlow(document, placeOrder, INPUTS_NODE_ID, 'create', ['sku'], documents)

    // Assert
    expect(total!.link).toEqual({ label: 'Schema money', navId: 'orders/model/money' })
    // `order` has no model entry in this sidebar, so the id links to the operation instead.
    expect(orderId!.link).toEqual({ label: 'POST /orders', navId: 'orders/tag/orders/POST/orders' })
    expect(sku!.link).toEqual({ label: "The workflow's inputs", anchor: 'doc/workflow/placeOrder/inputs/sku' })
    expect(unlinked!.link).toBeUndefined()
  })

  it('links a property to the named response schema it is in, when Scalar lists that schema', () => {
    // Arrange
    const navigation = ordersApi['x-scalar-navigation']
    const withOrder = {
      ...documents,
      orders: { ...ordersApi, 'x-scalar-navigation': { children: [...navigation.children, { type: 'model', id: 'orders/model/order', ref: '#/components/schemas/order' }] } },
    }

    // Act
    const [orderId] = describeFlow(document, placeOrder, 'create', OUTPUTS_NODE_ID, ['orderId'], withOrder)

    // Assert
    expect(orderId!.link).toEqual({ label: 'Schema order', navId: 'orders/model/order' })
    expect(orderId!.schema).toMatchObject({ type: 'string', details: ['uuid'] })
  })

  it('reads a Selector Object output by its context and JSONPath', () => {
    // Arrange
    const create = { ...placeOrder.steps![0]!, outputs: { amount: { context: '$response.body', selector: "$.total['amount']", type: 'jsonpath' } } }
    const filtered = { ...create, outputs: { amount: { context: '$response.body', selector: '$.lines[?(@.sku)]', type: 'jsonpath' } } }
    const withSelector = (step: typeof create): Workflow => ({ ...placeOrder, steps: [step as never], outputs: { amount: '$steps.create.outputs.amount' } })

    // Act
    const [amount] = describeFlow(document, withSelector(create), 'create', OUTPUTS_NODE_ID, ['amount'], documents)
    const [unknown] = describeFlow(document, withSelector(filtered), 'create', OUTPUTS_NODE_ID, ['amount'], documents)

    // Assert
    expect(amount).toMatchObject({
      source: "$response.body · $.total['amount']",
      origin: '201 response body of POST /orders',
      schema: { type: 'number' },
      link: { label: 'Schema money', navId: 'orders/model/money' },
    })
    // A JSONPath with a filter can't be followed into the schema: it still says where it reads from.
    expect(unknown).toMatchObject({ source: '$response.body · $.lines[?(@.sku)]', origin: '201 response body of POST /orders' })
    expect(unknown!.schema).toBeUndefined()
  })

  it('still says where a value comes from when its document is not loaded', () => {
    // Act
    const [orderId] = describeFlow(document, placeOrder, 'create', 'paid', ['orderId'], {})

    // Assert
    expect(orderId).toMatchObject({ source: '$response.body#/id', origin: 'the response body', uses: ['query parameter order', 'correlation'] })
    expect(orderId!.schema).toBeUndefined()
  })
})

describe('the Data flow details', () => {
  let wrapper: VueWrapper | undefined

  afterEach(() => {
    wrapper?.unmount()
    wrapper = undefined
    localStorage.clear()
    resetWorkflowView()
  })

  it('lists each value with where it is set from, where it is used and its schema', () => {
    // Arrange
    const values = describeFlow(document, placeOrder, 'create', 'paid', ['orderId'], documents)

    // Act
    wrapper = mount(ArazzoDataFlowDetails, { props: { from: 'create', to: 'paid', values } })

    // Assert
    expect(wrapper.text()).toContain('From create to paid')
    expect(wrapper.find('.ba-flow-facts').text()).toContain('$response.body#/id')
    expect(wrapper.find('.ba-flow-facts').text()).toContain('query parameter order, correlation')
    expect(wrapper.find('.ba-schema-name').text()).toBe('orderId')
    expect(wrapper.find('.ba-flow-no-schema').exists()).toBe(false)
  })

  it('links each schema to where it is defined, and asks the dialogs to close when a link is followed', async () => {
    // Arrange
    const onNavigate = vi.fn()
    const values = [
      ...describeFlow(document, placeOrder, 'create', OUTPUTS_NODE_ID, ['total'], documents),
      ...describeFlow(document, placeOrder, INPUTS_NODE_ID, 'create', ['sku'], documents, 'doc/workflow/placeOrder'),
    ]
    wrapper = mount(ArazzoDataFlowDetails, { attachTo: globalThis.document.body, props: { from: 'create', to: 'Workflow outputs', values, onNavigate } })
    const [schema, input] = wrapper.findAll('a.ba-flow-link')

    // Act
    await input!.trigger('click')

    // Assert
    expect(schema!.text()).toBe('Schema money ↗')
    expect(schema!.attributes('href')).toMatch(/#orders\/model\/money$/)
    expect(input!.text()).toBe("The workflow's inputs ↗")
    expect(input!.attributes('href')).toMatch(/#doc\/workflow\/placeOrder\/inputs\/sku$/)
    expect(onNavigate).toHaveBeenCalledOnce()
  })

  it.each(['click', 'Enter'])('opens from an arrow label of the Data flow view (%s)', async (how) => {
    // Arrange
    setWorkflowView('data')
    const [view] = toWorkflowViews(document, traverseArazzoDocument({ documentId: 'doc', document: document as unknown as Record<string, unknown> }))
    wrapper = mount(ArazzoWorkflow, { attachTo: globalThis.document.body, props: { view: view!, document, documents } })
    await flushPromises()
    const dialog = wrapper.find('dialog.ba-flow-dialog').element as HTMLDialogElement
    const showModal = vi.spyOn(dialog, 'showModal').mockImplementation(() => undefined)
    const label = wrapper.findAll('.ba-overview .ba-diagram-edge-label.ba-diagram-link').find((candidate) => candidate.find('text').text() === 'orderId')!

    // Act
    await (how === 'click' ? label.trigger('click') : label.trigger('keydown', { key: 'Enter' }))
    await flushPromises()

    // Assert
    expect(label.attributes('role')).toBe('button')
    expect(showModal).toHaveBeenCalled()
    expect(wrapper.find('.ba-flow-dialog').text()).toContain('From create to paid')
    expect(wrapper.find('.ba-flow-dialog .ba-schema-name').text()).toBe('orderId')
  })
})
