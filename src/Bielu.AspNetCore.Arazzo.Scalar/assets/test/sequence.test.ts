// @vitest-environment happy-dom
import { DOMWrapper, flushPromises, mount, type VueWrapper } from '@vue/test-utils'
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'
import ArazzoSequenceDiagram from '../src/components/ArazzoSequenceDiagram.vue'
import ArazzoWorkflow from '../src/components/ArazzoWorkflow.vue'
import { successPath } from '../src/helpers/execution-order'
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
  // trackOrder has no steps of its own here, so the step invoking it is a call into its lane rather than a stage.
  workflows: [placeOrder, { workflowId: 'trackOrder' }],
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
  it('stops at an unconditional end, and follows a goto', () => {
    // Arrange
    const workflow: Workflow = {
      workflowId: 'w',
      steps: [
        { stepId: 'start', operationId: 'a', onSuccess: [{ name: 'done', type: 'end' }] },
        { stepId: 'later', operationId: 'b' },
      ],
    }
    const jumping: Workflow = {
      workflowId: 'j',
      steps: [
        { stepId: 'start', operationId: 'a', onSuccess: [{ name: 'skip', type: 'goto', stepId: 'last' }] },
        { stepId: 'skipped', operationId: 'b' },
        { stepId: 'last', operationId: 'c' },
      ],
    }
    const branching: Workflow = {
      workflowId: 'b',
      steps: [
        { stepId: 'start', operationId: 'a', onSuccess: [{ name: 'early', type: 'end', criteria: [{ condition: '$statusCode == 204' }] }] },
        { stepId: 'later', operationId: 'b' },
      ],
    }
    const stepsOf = (candidate: Workflow) => [
      ...new Set(buildSequence({ arazzo: '1.1.0', workflows: [candidate] }, candidate, {}).messages.map((message) => message.stepId)),
    ]

    // Act / Assert
    expect(stepsOf(workflow)).toEqual(['start'])
    expect(stepsOf(jumping)).toEqual(['start', 'last'])
    // A conditional end may not apply: the run can carry on.
    expect(stepsOf(branching)).toEqual(['start', 'later'])
  })

  it('draws the steps in the order the run reaches them, not the order the document lists them', () => {
    // Arrange: a jumps forward to c, which jumps back to b, which ends.
    const workflow: Workflow = {
      workflowId: 'w',
      steps: [
        { stepId: 'a', operationId: 'a', onSuccess: [{ name: 'toC', type: 'goto', stepId: 'c' }] },
        { stepId: 'b', operationId: 'b', onSuccess: [{ name: 'done', type: 'end' }] },
        { stepId: 'c', operationId: 'c', onSuccess: [{ name: 'toB', type: 'goto', stepId: 'b' }] },
      ],
    }
    const owner: ArazzoDocument = { arazzo: '1.1.0', workflows: [workflow] }

    // Act
    const order = buildSequence(owner, workflow, {}).messages.map((message) => message.stepId)

    // Assert: each step is drawn whole (its request and response) before the next.
    expect([...new Set(order)]).toEqual(['a', 'c', 'b'])
    expect(order.indexOf('c')).toBeLessThan(order.indexOf('b'))
    expect(successPath(owner, workflow).map((step) => step.stepId)).toEqual(['a', 'c', 'b'])
  })

  it('follows the run that carries on first, then each conditional goto, placing a shared step where the run reaches it first', () => {
    // Arrange: `start` may jump to `shared` or `side`; carrying on, it reaches `middle` and then `shared` too.
    const workflow: Workflow = {
      workflowId: 'w',
      steps: [
        {
          stepId: 'start',
          operationId: 'a',
          onSuccess: [
            { name: 'skip', type: 'goto', stepId: 'shared', criteria: [{ condition: '$statusCode == 202' }] },
            { name: 'aside', type: 'goto', stepId: 'side', criteria: [{ condition: '$statusCode == 203' }] },
          ],
        },
        { stepId: 'middle', operationId: 'b' },
        { stepId: 'shared', operationId: 'c', onSuccess: [{ name: 'done', type: 'end' }] },
        { stepId: 'side', operationId: 'd' },
      ],
    }

    // Act
    const order = successPath({ arazzo: '1.1.0', workflows: [workflow] }, workflow).map((step) => step.stepId)

    // Assert
    expect(order).toEqual(['start', 'middle', 'shared', 'side'])
  })

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
    expect(buildSequence(document, { workflowId: 'empty' }, documents)).toEqual({ participants: [{ id: CLIENT_ID, label: 'Client' }], messages: [], stages: [] })
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

  it('keeps five lanes readable on a phone, scrolling sideways rather than shrinking the labels', () => {
    // Arrange
    const participants = [{ id: CLIENT_ID, label: 'Client' }, ...['a', 'b', 'c', 'd'].map((id) => ({ id, label: id }))]
    const sequence = { participants, messages: [{ stepId: 's', from: CLIENT_ID, to: 'd', label: 'GET /x', kind: 'request' as const }] }

    // Act
    wrapper = mount(ArazzoSequenceDiagram, { props: { sequence, stepSectionIds: {} } })

    // Assert
    const svg = wrapper.find('svg').element as SVGSVGElement
    const natural = Number(svg.getAttribute('viewBox')!.split(' ')[2])
    // At 390px the column is far narrower than this: the 11px labels stay at 85% or more, and the container scrolls.
    expect(Number.parseFloat(svg.style.minWidth) / natural).toBeGreaterThanOrEqual(0.85)
    expect(natural).toBeGreaterThan(390)
  })

  it.each(['Enter', ' '])('opens a step from the full-window sequence with %j: closes the window and focuses the step', async (key) => {
    // Arrange
    const [view] = toWorkflowViews(document, traverseArazzoDocument({ documentId: 'doc', document: document as unknown as Record<string, unknown> }))
    wrapper = mount(ArazzoWorkflow, { attachTo: globalThis.document.body, props: { view: view!, document, documents } })
    await wrapper.findAll('.ba-overview .ba-view-tabs button').find((button) => button.text() === 'Sequence')!.trigger('click')
    await wrapper.find('button[aria-label="Expand workflow diagram"]').trigger('click')
    await flushPromises()
    // ScalarModal renders the full window in a portal at the end of <body>, only while it is open.
    const step = new DOMWrapper(globalThis.document.querySelector('[role="dialog"] .ba-sequence-step')!)
    const section = globalThis.document.getElementById(view!.steps[0]!.id)!
    section.scrollIntoView = vi.fn()

    // Act
    await step.trigger('keydown', { key })
    await flushPromises()

    // Assert
    expect(globalThis.document.querySelector('[role="dialog"]')).toBeNull()
    expect(globalThis.document.activeElement).toBe(section)
  })

  it('replaces the overview when Sequence is chosen, without export or legend', async () => {
    // Arrange
    localStorage.clear()
    resetWorkflowView()
    const [view] = toWorkflowViews(document, traverseArazzoDocument({ documentId: 'doc', document: document as unknown as Record<string, unknown> }))
    wrapper = mount(ArazzoWorkflow, { props: { view: view!, document, documents } })

    // Act
    await wrapper.findAll('.ba-overview .ba-view-tabs button').find((button) => button.text() === 'Sequence')!.trigger('click')
    await flushPromises()

    // Assert
    expect(wrapper.find('.ba-overview .ba-sequence').exists()).toBe(true)
    expect(wrapper.find('.ba-overview .ba-diagram').exists()).toBe(false)
    expect(wrapper.find('.ba-overview .ba-export').exists()).toBe(false)
    expect(wrapper.find('.ba-overview .ba-diagram-legend').exists()).toBe(false)
  })
})

describe('stages', () => {
  const pay: Workflow = {
    workflowId: 'takePayment',
    summary: 'Take the payment',
    steps: [
      { stepId: 'create', operationId: 'createOrder', successCriteria: [{ condition: '$statusCode == 201' }] },
      { stepId: 'paid', channelPath: '{$sourceDescriptions.events.url}#/channels/paymentCompleted', action: 'receive' },
      { stepId: 'deeper', workflowId: 'audit' },
    ],
  }
  const checkout: Workflow = {
    workflowId: 'checkout',
    steps: [
      { stepId: 'payment', workflowId: 'takePayment', onFailure: [{ name: 'declined', type: 'end', criteria: [{ condition: '$statusCode == 402' }] }] },
      { stepId: 'track', workflowId: 'noSummary' },
      { stepId: 'empty', workflowId: 'nothing' },
    ],
  }
  const staged: ArazzoDocument = {
    ...document,
    workflows: [
      checkout,
      pay,
      { workflowId: 'audit', steps: [{ stepId: 'log', operationId: 'createOrder' }] },
      { workflowId: 'noSummary', steps: [{ stepId: 'create', operationId: 'createOrder' }] },
      { workflowId: 'nothing' },
    ],
  }

  let wrapper: VueWrapper | undefined

  afterEach(() => {
    wrapper?.unmount()
    wrapper = undefined
  })

  it('draws an invoked workflow as a stage of its own steps, one level deep', () => {
    // Act
    const sequence = buildSequence(staged, checkout, documents)

    // Assert
    expect(sequence.stages).toEqual([
      { id: 'payment', workflowId: 'takePayment', label: 'Take the payment', note: 'on 402: declined (end)' },
      { id: 'track', workflowId: 'noSummary', label: 'noSummary' },
    ])
    expect(sequence.messages.map(({ stepId, kind, stage }) => [stepId, kind, stage])).toEqual([
      ['create', 'request', 'payment'],
      ['create', 'response', 'payment'],
      ['paid', 'receive', 'payment'],
      // A workflow the stage invokes in turn is a call into its lane, not a stage inside the stage.
      ['deeper', 'invoke', 'payment'],
      ['create', 'request', 'track'],
      ['create', 'response', 'track'],
      // A workflow without steps is a call into its lane, as before.
      ['empty', 'invoke', undefined],
    ])
    expect(sequence.participants.map((participant) => participant.id)).toEqual([
      CLIENT_ID,
      'source:orders',
      'source:events',
      'workflow:audit',
      'workflow:nothing',
    ])
  })

  it('draws only the steps a successful run of the invoked workflow reaches', () => {
    // Arrange
    const short: ArazzoDocument = {
      arazzo: '1.1.0',
      workflows: [
        { workflowId: 'outer', steps: [{ stepId: 'inner', workflowId: 'inner' }] },
        {
          workflowId: 'inner',
          steps: [
            { stepId: 'first', operationId: 'a', onSuccess: [{ name: 'done', type: 'end' }] },
            { stepId: 'never', operationId: 'b' },
          ],
        },
      ],
    }

    // Act
    const sequence = buildSequence(short, short.workflows![0], {})

    // Assert
    expect([...new Set(sequence.messages.map((message) => message.stepId))]).toEqual(['first'])
  })

  it('bands each stage, numbered in run order, and opens the invoking step from the stage and its steps', async () => {
    // Arrange
    const host = globalThis.document.createElement('div')
    host.innerHTML = '<section id="doc/step/payment"></section><section id="doc/step/create"></section>'
    globalThis.document.body.appendChild(host)
    const payment = host.querySelector<HTMLElement>('[id="doc/step/payment"]')!
    const create = host.querySelector<HTMLElement>('[id="doc/step/create"]')!
    payment.scrollIntoView = vi.fn()
    create.scrollIntoView = vi.fn()
    // `create` is also a step id of this workflow: a stage's step must not open it.
    wrapper = mount(ArazzoSequenceDiagram, {
      attachTo: globalThis.document.body,
      props: { sequence: buildSequence(staged, checkout, documents), stepSectionIds: { payment: 'doc/step/payment', create: 'doc/step/create' } },
    })

    // Act
    await wrapper.find('.ba-sequence-step').trigger('click')
    await flushPromises()

    // Assert
    const titles = wrapper.findAll('.ba-sequence-stage-title text').map((title) => title.text())
    expect(titles).toEqual(['1 · Take the payment', '2 · noSummary'])
    expect(wrapper.find('.ba-sequence-stage .ba-sequence-note').text()).toBe('on 402: declined (end)')
    expect(wrapper.findAll('.ba-sequence-step text').map((step) => step.text())).toEqual(['create', 'paid', 'deeper', 'create', 'empty'])
    expect(payment.scrollIntoView).toHaveBeenCalled()
    expect(create.scrollIntoView).not.toHaveBeenCalled()
    // The second stage's steps have no section to open: their invoking step `track` has none here.
    expect(wrapper.findAll('.ba-sequence-step')[3]!.classes()).not.toContain('ba-diagram-link')
    // Each band encloses its rows, and the diagram is tall enough for the last one.
    const [first, second] = wrapper.findAll('.ba-sequence-stage > rect').map((rect) => ({
      top: Number(rect.attributes('y')),
      bottom: Number(rect.attributes('y')) + Number(rect.attributes('height')),
    }))
    const rows = wrapper.findAll('.ba-sequence-line').map((line) => Number(line.attributes('y1')))
    expect(rows.slice(0, 4).every((y) => y > first!.top && y < first!.bottom)).toBe(true)
    expect(rows.slice(4, 6).every((y) => y > second!.top && y < second!.bottom)).toBe(true)
    expect(rows[6]).toBeGreaterThan(second!.bottom)
    expect(Number(wrapper.find('svg').attributes('viewBox')!.split(' ')[3])).toBeGreaterThan(rows[6]!)
    host.remove()
  })

  it('opens the invoking step from a stage title with the keyboard', async () => {
    // Arrange
    const host = globalThis.document.createElement('div')
    host.innerHTML = '<section id="doc/step/payment"></section>'
    globalThis.document.body.appendChild(host)
    const payment = host.querySelector('section')!
    payment.scrollIntoView = vi.fn()
    wrapper = mount(ArazzoSequenceDiagram, {
      attachTo: globalThis.document.body,
      props: { sequence: buildSequence(staged, checkout, documents), stepSectionIds: { payment: 'doc/step/payment' } },
    })

    // Act
    await wrapper.find('.ba-sequence-stage-title').trigger('keydown', { key: 'Enter' })
    await flushPromises()

    // Assert
    expect(payment.scrollIntoView).toHaveBeenCalled()
    host.remove()
  })
})

describe('notes', () => {
  it('reads an input a stage\'s step is matched on as coming from the inputs', () => {
    // Arrange
    const outer: Workflow = { workflowId: 'outer', steps: [{ stepId: 'pay', workflowId: 'inner' }] }
    const inner: Workflow = {
      workflowId: 'inner',
      steps: [{ stepId: 'paid', channelPath: '{$sourceDescriptions.events.url}#/channels/paymentCompleted', action: 'receive', correlationId: '$inputs.orderId' }],
    }

    // Act
    const sequence = buildSequence({ ...document, workflows: [outer, inner] }, outer, documents)

    // Assert
    expect(sequence.messages[0]).toMatchObject({ stage: 'pay', note: 'matched on orderId from inputs' })
  })
})
