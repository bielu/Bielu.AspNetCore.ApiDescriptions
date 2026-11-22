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

  it('follows a Selector Object that forwards another output by its context, then its selector', () => {
    // Arrange: `paid` passes on part of `create`'s total.
    const paid = { ...placeOrder.steps![1]!, outputs: { amount: { context: '$steps.create.outputs.total', selector: '$.amount', type: 'jsonpath' } } }
    const xpath = { ...paid, outputs: { amount: { context: '$steps.create.outputs.total', selector: '/amount', type: 'xpath' } } }
    const forwarding = (step: typeof paid): Workflow => ({ ...placeOrder, steps: [placeOrder.steps![0]!, step as never], outputs: { amount: '$steps.paid.outputs.amount' } })

    // Act
    const [amount] = describeFlow(document, forwarding(paid), 'paid', OUTPUTS_NODE_ID, ['amount'], documents)
    const [unknown] = describeFlow(document, forwarding(xpath), 'paid', OUTPUTS_NODE_ID, ['amount'], documents)

    // Assert: described where the context comes from, narrowed by the selector; the display text keeps both.
    expect(amount).toMatchObject({
      source: '$steps.create.outputs.total · $.amount',
      origin: '201 response body of POST /orders',
      schema: { type: 'number' },
      link: { label: 'Schema money', navId: 'orders/model/money' },
    })
    // A selector this cannot follow still says where the context comes from, but not the selected part's schema.
    expect(unknown).toMatchObject({ origin: '201 response body of POST /orders' })
    expect(unknown!.schema).toBeUndefined()
  })

  it("resolves the inputs schema's own definitions, read directly or through a pointer", () => {
    // Arrange: `order` refers to a definition inside the inputs schema, which the Arazzo document doesn't have.
    const inputs = {
      type: 'object',
      properties: { order: { $ref: '#/$defs/Order' } },
      $defs: { Order: { type: 'object', properties: { id: { type: 'string', format: 'uuid' } } } },
    }
    const withDefs: Workflow = {
      workflowId: 'withDefs',
      inputs,
      steps: [{ stepId: 'echo', operationId: 'x', requestBody: { payload: '$inputs.order' }, outputs: { orderId: '$inputs.order#/id' } }],
      outputs: { orderId: '$steps.echo.outputs.orderId' },
    }
    const owner: ArazzoDocument = { arazzo: '1.1.0', workflows: [withDefs] }

    // Act
    const [order] = describeFlow(owner, withDefs, INPUTS_NODE_ID, 'echo', ['order'], {})
    const [orderId] = describeFlow(owner, withDefs, 'echo', OUTPUTS_NODE_ID, ['orderId'], {})

    // Assert
    expect(order!.schema).toMatchObject({ type: 'object' })
    expect(order!.schema!.children!.map((row) => row.name)).toEqual(['id'])
    expect(orderId).toMatchObject({ origin: "the workflow's inputs", schema: { type: 'string', details: ['uuid'] } })
  })

  it('keeps the schema an external response reference resolved to, and the reference for its link', () => {
    // Arrange: Scalar resolved an external `$ref` and put the result next to it.
    const external = structuredClone(ordersApi) as typeof ordersApi & Record<string, unknown>
    const content = external.paths['/orders'].post.responses['201'].content!['application/json'] as { schema: Record<string, unknown> }
    content.schema = { $ref: 'https://schemas.test/order.json', '$ref-value': ordersApi.components.schemas.order }

    // Act
    const [orderId] = describeFlow(document, placeOrder, 'create', OUTPUTS_NODE_ID, ['orderId'], { ...documents, orders: external })

    // Assert
    expect(orderId).toMatchObject({ origin: '201 response body of POST /orders', schema: { type: 'string', details: ['uuid'] } })
  })

  it("says where a step's success and failure actions read a value", () => {
    // Arrange: `paid` reads the order id only in its actions.
    const paid = {
      ...placeOrder.steps![1]!,
      parameters: [],
      correlationId: undefined,
      onSuccess: [{ name: 'shipped', type: 'end', criteria: [{ condition: '$steps.create.outputs.orderId != null' }] }],
      onFailure: [{ reference: '$components.failureActions.retryOrder' }],
    }
    const owner: ArazzoDocument = {
      ...document,
      components: {
        ...document.components,
        failureActions: {
          retryOrder: { name: 'retryOrder', type: 'goto', workflowId: 'retry', parameters: [{ name: 'order', value: '$steps.create.outputs.orderId' }] },
        },
      },
    }
    const workflow: Workflow = { ...placeOrder, steps: [placeOrder.steps![0]!, paid as never] }

    // Act
    const [orderId] = describeFlow({ ...owner, workflows: [workflow] }, workflow, 'create', 'paid', ['orderId'], documents)

    // Assert
    expect(orderId!.uses).toEqual(['success action shipped criteria', 'failure action retryOrder parameter order'])
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

  it('moves the focus to the input row when its link is followed from the keyboard, not by pointer', async () => {
    // Arrange
    const row = globalThis.document.createElement('div')
    row.id = 'doc/workflow/placeOrder/inputs/sku'
    globalThis.document.body.append(row)
    const values = describeFlow(document, placeOrder, INPUTS_NODE_ID, 'create', ['sku'], documents, 'doc/workflow/placeOrder')
    wrapper = mount(ArazzoDataFlowDetails, { attachTo: globalThis.document.body, props: { from: 'Inputs', to: 'create', values } })
    const link = wrapper.find('a.ba-flow-link')

    try {
      // Act: a pointer click (`detail` 1) scrolls only; Enter on the link (`detail` 0) also moves the focus.
      await link.trigger('click', { detail: 1 })
      await flushPromises()
      const afterPointer = globalThis.document.activeElement
      await link.trigger('click', { detail: 0 })
      await flushPromises()

      // Assert
      expect(afterPointer).not.toBe(row)
      expect(globalThis.document.activeElement).toBe(row)
    } finally {
      row.remove()
    }
  })

  it('marks each Data flow arrow label with an info icon, with room for it, and no other view', async () => {
    // Arrange
    const [view] = toWorkflowViews(document, traverseArazzoDocument({ documentId: 'doc', document: document as unknown as Record<string, unknown> }))
    const mountIn = async (overview: 'data' | 'technical') => {
      setWorkflowView(overview)
      const mounted = mount(ArazzoWorkflow, { props: { view: view!, document, documents } })
      await flushPromises()
      return mounted
    }

    // Act
    wrapper = await mountIn('data')
    const labels = wrapper.findAll('.ba-overview .ba-diagram-edge-label')
    const sku = labels.find((label) => label.find('text').text() === 'sku')!
    const technical = await mountIn('technical')

    // Assert
    expect(labels.length).toBeGreaterThan(0)
    expect(labels.every((label) => label.find('.ba-diagram-flow-icon').exists())).toBe(true)
    expect(sku.find('.ba-diagram-flow-icon').attributes('aria-hidden')).toBe('true')
    // "sku": three characters at 6.2px, 12px of padding and 14px for the icon.
    expect(Number(sku.find('rect').attributes('width'))).toBeCloseTo(3 * 6.2 + 12 + 14, 1)
    expect(technical.find('.ba-overview .ba-diagram-flow-icon').exists()).toBe(false)
    technical.unmount()
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
