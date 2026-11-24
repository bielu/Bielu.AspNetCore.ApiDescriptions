// @vitest-environment happy-dom
import { DOMWrapper, flushPromises, mount, type VueWrapper } from '@vue/test-utils'
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'
import ArazzoSequenceDiagram from '../src/components/ArazzoSequenceDiagram.vue'
import ArazzoWorkflow from '../src/components/ArazzoWorkflow.vue'
import { successPath } from '../src/helpers/execution-order'
import { toWorkflowViews, traverseArazzoDocument } from '../src/helpers/navigation'
import { pluginOptionsFromModuleUrl, resolvePluginOptions } from '../src/helpers/plugin-options'
import { actorRegistry, buildSequence, CLIENT_ID, parallelGroups, runOrder } from '../src/helpers/sequence'
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
      // create can end two ways: an alt with the 201 first, then the 400 its failure action handles.
      { stepId: 'create', from: 'source:orders', to: CLIENT_ID, label: '201', kind: 'response', operand: { alt: '/create', index: 0, guard: '201' } },
      {
        stepId: 'create',
        from: 'source:orders',
        to: CLIENT_ID,
        label: '400',
        kind: 'response',
        note: 'rejected: end',
        operand: { alt: '/create', index: 1, guard: '400' },
      },
      { stepId: 'announce', from: CLIENT_ID, to: 'source:events', label: 'orderPlaced', kind: 'send' },
      { stepId: 'paid', from: 'source:events', to: CLIENT_ID, label: 'paymentCompleted', kind: 'receive', note: 'waits up to 1 min, matched on orderId from create' },
      { stepId: 'track', from: CLIENT_ID, to: 'workflow:trackOrder', label: 'runs the workflow', kind: 'invoke' },
    ])
  })

  it('draws every way a step can end as an operand of its alt, under the condition leading there', () => {
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
            { name: 'e', type: 'retry', retryAfter: 2, retryLimit: 3, criteria: [{ condition: '$statusCode == 503' }] },
            { name: 'd', type: 'end' },
          ],
          onSuccess: [
            { name: 'cached', type: 'goto', stepId: 'create', criteria: [{ condition: '$statusCode == 304' }] },
            { name: 'done', type: 'end' },
          ],
        },
      ],
    }

    // Act
    const [, ...ends] = buildSequence(document, workflow, documents).messages

    // Assert: the success path ("response": no status in the criteria), then the conditional success action, then
    // each failure action; a failure with no status comes back as an error.
    expect(ends.map(({ label, note, operand }) => [operand?.index, operand?.guard, label, note])).toEqual([
      [0, 'success', 'response', undefined],
      [1, '304', '304', 'cached: → create'],
      [2, '400', '400', 'a: end'],
      [3, 'if $response.body#/retry == true', 'error', 'b: → create'],
      [4, '409', '409', 'c: → other'],
      [5, '503', '503', 'e: retry after 2 s × 3'],
      [6, 'otherwise', 'error', 'd: end'],
    ])
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
    expect(buildSequence(document, { workflowId: 'empty' }, documents)).toEqual({ participants: [{ id: CLIENT_ID, label: 'Client' }], messages: [], stages: [], parallels: [] })
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
    expect(wrapper.findAll('.ba-sequence-message')).toHaveLength(6)
    expect(wrapper.findAll('.ba-sequence-step text').map((step) => step.text())).toEqual(['create', 'announce', 'paid', 'track'])
    expect(wrapper.find('.ba-sequence-response .ba-sequence-line').classes()).toContain('ba-sequence-dashed')
    expect(wrapper.find('.ba-sequence-receive .ba-sequence-note').text()).toBe('waits up to 1 min, matched on orderId from create')
    expect(section.scrollIntoView).toHaveBeenCalled()
    host.remove()
  })

  it('keeps five lanes readable on a phone, scrolling sideways rather than shrinking the labels', () => {
    // Arrange
    const participants = [{ id: CLIENT_ID, label: 'Client' }, ...['a', 'b', 'c', 'd'].map((id) => ({ id, label: id }))]
    const sequence = { participants, messages: [{ stepId: 's', from: CLIENT_ID, to: 'd', label: 'GET /x', kind: 'request' as const }], stages: [], parallels: [] }

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
      // How the invoking step ends: on the stage's boundary, the success path and then `declined`.
      ['payment', 'response', undefined],
      ['payment', 'response', undefined],
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

  it('keeps the invoking step\'s own outcomes whether or not its workflow\'s document is loaded', () => {
    // Arrange: `settle` lives in another document; `alreadyPaid` is the caller's conditional success action.
    const billing = {
      arazzo: '1.1.0',
      'x-scalar-original-source-url': '/billing.arazzo.json',
      sourceDescriptions: [{ name: 'orders', url: '/openapi.json', type: 'openapi' }],
      workflows: [{ workflowId: 'settle', steps: [{ stepId: 'charge', operationId: 'createOrder' }] }],
    }
    const caller: Workflow = {
      workflowId: 'checkout',
      steps: [
        {
          stepId: 'pay',
          workflowId: '$sourceDescriptions.billing.settle',
          onSuccess: [{ name: 'alreadyPaid', type: 'end', criteria: [{ condition: '$outputs.alreadyPaid == true' }] }],
        },
      ],
    }
    const owner: ArazzoDocument = {
      arazzo: '1.1.0',
      sourceDescriptions: [{ name: 'billing', url: '/billing.arazzo.json', type: 'arazzo' }],
      workflows: [caller],
    }
    const outcomes = (sequence: ReturnType<typeof buildSequence>) =>
      sequence.messages.filter((message) => message.stepId === 'pay').map(({ operand, note }) => [operand?.guard, note])

    // Act
    const pending = buildSequence(owner, caller, {})
    const loaded = buildSequence(owner, caller, { ordersApi, billing })

    // Assert: before the document loads, the call into the workflow's lane carries the alt; after, the stage's
    // boundary does, with the same operands.
    expect(pending.stages).toEqual([])
    expect(loaded.stages.map((stage) => stage.id)).toEqual(['pay'])
    const expected = [
      ['success', undefined],
      ['if $outputs.alreadyPaid == true', 'alreadyPaid: end'],
    ]
    expect(outcomes(pending)).toEqual(expected)
    expect(outcomes(loaded)).toEqual(expected)
    expect(loaded.messages.map(({ stepId, stage }) => [stepId, stage])).toEqual([
      ['charge', 'pay'],
      ['charge', 'pay'],
      ['pay', undefined],
      ['pay', undefined],
    ])

    // Act: the expanded stage as drawn.
    wrapper = mount(ArazzoSequenceDiagram, { props: { sequence: loaded, stepSectionIds: {} } })

    // Assert: the alt is below the stage's band, and shows the caller's action.
    const band = wrapper.find('.ba-sequence-stage rect')
    const alt = wrapper.find('.ba-sequence-alt')
    expect(alt.findAll('.ba-sequence-guard').map((guard) => guard.text())).toEqual(['[success]', '[if $outputs.alreadyPaid == true]'])
    expect(Number(alt.find('rect').attributes('y'))).toBeGreaterThan(Number(band.attributes('y')) + Number(band.attributes('height')))
    expect(wrapper.text()).toContain('alreadyPaid: end')
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
    // `payment` again below its stage: how the invoking step ends.
    expect(wrapper.findAll('.ba-sequence-step text').map((step) => step.text())).toEqual(['create', 'paid', 'deeper', 'payment', 'create', 'empty'])
    expect(payment.scrollIntoView).toHaveBeenCalled()
    expect(create.scrollIntoView).not.toHaveBeenCalled()
    // The second stage's steps have no section to open: their invoking step `track` has none here.
    expect(wrapper.findAll('.ba-sequence-step')[4]!.classes()).not.toContain('ba-diagram-link')
    // Each band encloses its rows, and the diagram is tall enough for the last one.
    const [first, second] = wrapper.findAll('.ba-sequence-stage > rect').map((rect) => ({
      top: Number(rect.attributes('y')),
      bottom: Number(rect.attributes('y')) + Number(rect.attributes('height')),
    }))
    // A lane's call to itself is a path starting 7px above its row.
    const rows = wrapper
      .findAll('.ba-sequence-line')
      .map((line) => Number(line.attributes('y1') ?? Number(/^M[\d.]+,([\d.]+)/.exec(line.attributes('d')!)![1]) + 7))
    expect(rows.slice(0, 4).every((y) => y > first!.top && y < first!.bottom)).toBe(true)
    // The invoking step's outcomes are between the bands, in neither.
    expect(rows.slice(4, 6).every((y) => y > first!.bottom && y < second!.top)).toBe(true)
    expect(rows.slice(6, 8).every((y) => y > second!.top && y < second!.bottom)).toBe(true)
    expect(rows[8]).toBeGreaterThan(second!.bottom)
    expect(Number(wrapper.find('svg').attributes('viewBox')!.split(' ')[3])).toBeGreaterThan(rows[8]!)
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

describe('actors', () => {
  const options = { actorExtension: 'x-actor' }
  const services: ArazzoDocument = {
    arazzo: '1.1.0',
    sourceDescriptions: [
      { name: 'orders', url: '/openapi.json', type: 'openapi' },
      { name: 'payments', url: '/payments.json', type: 'openapi' },
      { name: 'events', url: '/asyncapi.json', type: 'asyncapi' },
    ],
    workflows: [],
  }
  const checkout = {
    workflowId: 'checkout',
    steps: [
      { stepId: 'create', operationId: '$sourceDescriptions.orders.createOrder', successCriteria: [{ condition: '$statusCode == 201' }] },
      { stepId: 'authorize', operationId: '$sourceDescriptions.payments.authorize', 'x-actor': 'orders' },
      { stepId: 'authorized', channelPath: '{$sourceDescriptions.events.url}#/channels/paymentCompleted', action: 'receive', 'x-actor': 'orders' },
      { stepId: 'announce', channelPath: '{$sourceDescriptions.events.url}#/channels/orderPlaced', action: 'send', 'x-actor': 'orders' },
      { stepId: 'audit', operationId: '$sourceDescriptions.orders.createOrder', 'x-actor': 'orders' },
      { stepId: 'pick', operationId: '$sourceDescriptions.orders.createOrder', 'x-actor': ' warehouse ' },
      { stepId: 'blank', operationId: '$sourceDescriptions.orders.createOrder', 'x-actor': '  ' },
    ],
  } as unknown as Workflow

  it('starts each step from the lane its actor names, and sends what comes back there', () => {
    // Act
    const sequence = buildSequence({ ...services, workflows: [checkout] }, checkout, documents, options)

    // Assert
    expect(sequence.messages.map(({ stepId, from, to, kind }) => [stepId, kind, from, to])).toEqual([
      ['create', 'request', CLIENT_ID, 'source:orders'],
      ['create', 'response', 'source:orders', CLIENT_ID],
      ['authorize', 'request', 'source:orders', 'source:payments'],
      ['authorize', 'response', 'source:payments', 'source:orders'],
      ['authorized', 'receive', 'source:events', 'source:orders'],
      ['announce', 'send', 'source:orders', 'source:events'],
      // An actor that is also the step's target calls itself.
      ['audit', 'request', 'source:orders', 'source:orders'],
      ['audit', 'response', 'source:orders', 'source:orders'],
      // An actor that is no source description gets a lane of its own; a blank one is the client.
      ['pick', 'request', 'actor:warehouse', 'source:orders'],
      ['pick', 'response', 'source:orders', 'actor:warehouse'],
      ['blank', 'request', CLIENT_ID, 'source:orders'],
      ['blank', 'response', 'source:orders', CLIENT_ID],
    ])
    expect(sequence.participants.map(({ id, label }) => [id, label])).toEqual([
      [CLIENT_ID, 'Client'],
      ['source:orders', 'orders'],
      ['source:payments', 'payments'],
      ['source:events', 'events'],
      ['actor:warehouse', 'warehouse'],
    ])
  })

  it('ignores the extension unless the option names it', () => {
    // Act
    const sequence = buildSequence({ ...services, workflows: [checkout] }, checkout, documents)

    // Assert
    expect(sequence.messages.filter((message) => message.kind === 'request').every((message) => message.from === CLIENT_ID)).toBe(true)
  })

  it('takes a workflow\'s actor for its steps, and a stage\'s from the step invoking it, the step\'s own first', () => {
    // Arrange
    const pay = {
      workflowId: 'takePayment',
      steps: [
        { stepId: 'authorize', operationId: '$sourceDescriptions.payments.authorize' },
        { stepId: 'notify', operationId: '$sourceDescriptions.orders.createOrder', 'x-actor': 'payments' },
      ],
    }
    const owned = { workflowId: 'confirm', 'x-actor': 'payments', steps: [{ stepId: 'capture', operationId: '$sourceDescriptions.orders.createOrder' }] }
    const outer = {
      workflowId: 'outer',
      steps: [
        { stepId: 'payment', workflowId: 'takePayment', 'x-actor': 'orders' },
        { stepId: 'confirmation', workflowId: 'confirm', 'x-actor': 'orders' },
      ],
    }
    const owner = { ...services, workflows: [outer, pay, owned] } as unknown as ArazzoDocument

    // Act
    const requests = buildSequence(owner, outer as unknown as Workflow, documents, options).messages.filter((message) => message.kind === 'request')

    // Assert
    expect(requests.map(({ stepId, from }) => [stepId, from])).toEqual([
      ['authorize', 'source:orders'],
      ['notify', 'source:payments'],
      ['capture', 'source:payments'],
    ])
  })

  it('leaves the client out when every step names an actor, and starts a workflow call from the actor', () => {
    // Arrange
    const workflow = {
      workflowId: 'w',
      'x-actor': 'orders',
      steps: [
        { stepId: 'authorize', operationId: '$sourceDescriptions.payments.authorize' },
        { stepId: 'elsewhere', workflowId: '$sourceDescriptions.other.flow' },
      ],
    } as unknown as Workflow

    // Act
    const sequence = buildSequence({ ...services, workflows: [workflow] }, workflow, documents, options)

    // Assert
    expect(sequence.participants.map((participant) => participant.id)).toEqual(['source:orders', 'source:payments', 'workflow:$sourceDescriptions.other.flow'])
    expect(sequence.messages.at(-1)).toMatchObject({ kind: 'invoke', from: 'source:orders' })
  })

  it('draws a lane calling itself as a loop', () => {
    // Arrange
    const sequence = {
      participants: [{ id: 'a', label: 'a' }],
      messages: [{ stepId: 's', from: 'a', to: 'a', label: 'GET /self', kind: 'request' as const }],
      stages: [],
      parallels: [],
    }

    // Act
    const wrapper = mount(ArazzoSequenceDiagram, { props: { sequence, stepSectionIds: {} } })

    // Assert
    expect(wrapper.find('path.ba-sequence-self').attributes('d')).toMatch(/^M\d+,\d+ h\d+ v14 h-\d+$/)
    expect(wrapper.find('line.ba-sequence-line').exists()).toBe(false)
    expect(wrapper.find('.ba-sequence-label').attributes('text-anchor')).toBe('start')
    wrapper.unmount()
  })
})

describe('the actor option', () => {
  afterEach(() => {
    localStorage.clear()
    resetWorkflowView()
  })

  it('is read from every input path, and only as a specification extension', () => {
    // Act / Assert
    expect(resolvePluginOptions({ actorExtension: 'x-actor' })).toEqual({ actorExtension: 'x-actor' })
    expect(resolvePluginOptions(undefined, { actorExtension: 'actor' })).toEqual({})
    expect(pluginOptionsFromModuleUrl('https://cdn.test/p.mjs?actorExtension=x-actor')).toEqual({ actorExtension: 'x-actor' })
  })

  it('reaches the Sequence view of a workflow', async () => {
    // Arrange
    const owned: ArazzoDocument = {
      ...document,
      workflows: [{ workflowId: 'w', steps: [{ stepId: 'create', operationId: 'createOrder', 'x-actor': 'storefront' } as ArazzoStep] }],
    }
    const [view] = toWorkflowViews(owned, traverseArazzoDocument({ documentId: 'doc', document: owned as unknown as Record<string, unknown> }))
    const wrapper = mount(ArazzoWorkflow, { props: { view: view!, document: owned, documents, arazzoOptions: { actorExtension: 'x-actor' } } })

    // Act
    await wrapper.findAll('.ba-overview .ba-view-tabs button').find((button) => button.text() === 'Sequence')!.trigger('click')
    await flushPromises()

    // Assert
    expect(wrapper.findAll('.ba-overview .ba-sequence-participant > text:first-of-type').map((text) => text.text())).toEqual(['storefront', 'orders'])
    wrapper.unmount()
  })
})

describe('the actor registry', () => {
  const options = { actorExtension: 'x-actor', actorsExtension: 'x-actors' }
  const workflow = {
    workflowId: 'w',
    steps: [
      { stepId: 'create', operationId: 'createOrder' },
      { stepId: 'pick', operationId: 'createOrder', 'x-actor': 'warehouse' },
      { stepId: 'announce', channelPath: '{$sourceDescriptions.events.url}#/channels/orderPlaced', action: 'send', 'x-actor': 'orders' },
      { stepId: 'again', operationId: 'createOrder', 'x-actor': 'storefront' },
    ],
  } as unknown as Workflow
  const registered = (actors: unknown): ArazzoDocument => ({ ...document, workflows: [workflow], 'x-actors': actors }) as ArazzoDocument

  it('titles the lanes it lists, puts them first in its order, and names the client', () => {
    // Arrange
    const owner = registered({
      events: { title: 'Message broker' },
      orders: { title: 'Orders service', description: 'Takes and tracks orders.' },
      storefront: { title: 'Storefront', description: 'The shop the customer uses.', client: true },
      unused: { title: 'Never drawn' },
    })

    // Act
    const sequence = buildSequence(owner, workflow, documents, options)

    // Assert: the client first, then the listed lanes in the registry's order, then the rest as first used.
    expect(sequence.participants.map(({ id, label, description }) => [id, label, description])).toEqual([
      [CLIENT_ID, 'Storefront', 'The shop the customer uses.'],
      ['source:events', 'Message broker', undefined],
      ['source:orders', 'Orders service', 'Takes and tracks orders.'],
      ['actor:warehouse', 'warehouse', undefined],
    ])
    // A step whose actor is the client is on the client's lane.
    expect(sequence.messages.find((message) => message.stepId === 'again')).toMatchObject({ from: CLIENT_ID })
  })

  it('keeps the client called Client when no entry is the client, and its name when the entry has no title', () => {
    // Act
    const unnamed = buildSequence(registered({ orders: { title: 'Orders service' } }), workflow, documents, options)
    const named = buildSequence(registered({ shop: { client: true } }), workflow, documents, options)

    // Assert
    expect(unnamed.participants[0]).toEqual({ id: CLIENT_ID, label: 'Client' })
    expect(named.participants[0]).toEqual({ id: CLIENT_ID, label: 'shop' })
  })

  it('reads only well-formed entries, and nothing without the option', () => {
    // Arrange
    const owner = registered({ a: { title: ' A ', description: 3, client: 'yes' }, b: 'not an entry', c: [], d: { title: '  ' } })

    // Act / Assert
    expect([...actorRegistry(owner, 'x-actors')]).toEqual([
      ['a', { title: 'A' }],
      ['d', {}],
    ])
    expect(actorRegistry(owner, undefined).size).toBe(0)
    expect(actorRegistry(registered(['a']), 'x-actors').size).toBe(0)
    expect(buildSequence(registered({ orders: { title: 'Orders service' } }), workflow, documents, { actorExtension: 'x-actor' }).participants.map((participant) => participant.label)).toContain('orders')
  })

  it('titles the client of a workflow without steps, and shows a lane\'s description in its tooltip', () => {
    // Arrange
    const owner = registered({ shop: { title: 'Shop', description: 'Where customers buy.', client: true } })

    // Act
    const empty = buildSequence(owner, { workflowId: 'empty' }, documents, options)
    const wrapper = mount(ArazzoSequenceDiagram, { props: { sequence: buildSequence(owner, workflow, documents, options), stepSectionIds: {} } })

    // Assert
    expect(empty.participants).toEqual([{ id: CLIENT_ID, label: 'Shop', description: 'Where customers buy.' }])
    expect(wrapper.find('.ba-sequence-participant title').text()).toBe('Shop — Where customers buy.')
    wrapper.unmount()
  })

  it('is an option of the plugin', () => {
    // Act / Assert
    expect(resolvePluginOptions({ actorsExtension: 'x-actors' })).toEqual({ actorsExtension: 'x-actors' })
    expect(pluginOptionsFromModuleUrl('https://cdn.test/p.mjs?actorsExtension=x-actors')).toEqual({ actorsExtension: 'x-actors' })
  })
})

describe('review fixes', () => {
  it('keeps an inherited actor on the invoking step\'s lane when the stage is another document\'s workflow', () => {
    // Arrange: the invoked workflow lives in another Arazzo document, which has no source called `orders`.
    const other = {
      arazzo: '1.1.0',
      'x-scalar-original-source-url': '/other.arazzo.json',
      sourceDescriptions: [{ name: 'api', url: '/openapi.json', type: 'openapi' }],
      workflows: [
        {
          workflowId: 'inner',
          steps: [
            { stepId: 'inherits', operationId: 'createOrder' },
            { stepId: 'names', operationId: 'createOrder', 'x-actor': 'warehouse' },
          ],
        },
      ],
    }
    const outer = { workflowId: 'outer', steps: [{ stepId: 'run', workflowId: '$sourceDescriptions.other.inner', 'x-actor': 'orders' }] } as unknown as Workflow
    const owner: ArazzoDocument = {
      arazzo: '1.1.0',
      sourceDescriptions: [
        { name: 'orders', url: '/openapi.json', type: 'openapi' },
        { name: 'other', url: '/other.arazzo.json', type: 'arazzo' },
      ],
      workflows: [outer],
    }

    // Act
    const sequence = buildSequence(owner, outer, { orders: ordersApi, other }, { actorExtension: 'x-actor' })

    // Assert: the inherited actor is the parent's `orders` lane, not a lane of its own; a named one is resolved in
    // the other document.
    const requests = sequence.messages.filter((message) => message.kind === 'request')
    expect(requests.map(({ stepId, from }) => [stepId, from])).toEqual([
      ['inherits', 'source:orders'],
      ['names', 'actor:warehouse'],
    ])
    expect(sequence.participants.some((participant) => participant.id === 'actor:orders')).toBe(false)
  })

  it('keeps two documents\' services apart when both call their source by the same name', () => {
    // Arrange: the parent's `api` is the shop, the invoked workflow's document calls the billing service `api`.
    const shop = {
      openapi: '3.1.0',
      info: { title: 'Shop' },
      'x-scalar-original-source-url': '/shop.json',
      paths: { '/order': { get: { operationId: 'getOrder' } } },
    }
    const billing = {
      openapi: '3.1.0',
      info: { title: 'Billing' },
      'x-scalar-original-source-url': '/billing.json',
      paths: { '/charge': { get: { operationId: 'charge' } } },
    }
    const payments = {
      arazzo: '1.1.0',
      'x-scalar-original-source-url': '/payments.arazzo.json',
      sourceDescriptions: [{ name: 'api', url: '/billing.json', type: 'openapi' }],
      workflows: [{ workflowId: 'pay', steps: [{ stepId: 'charge', operationPath: '{$sourceDescriptions.api.url}#/paths/~1charge/get' }] }],
    }
    const checkout = {
      workflowId: 'checkout',
      steps: [
        { stepId: 'order', operationPath: '{$sourceDescriptions.api.url}#/paths/~1order/get' },
        { stepId: 'pay', workflowId: '$sourceDescriptions.payments.pay', 'x-actor': 'cashier' },
      ],
    } as unknown as Workflow
    const owner = {
      arazzo: '1.1.0',
      'x-actors': { api: { title: 'Shop' } },
      sourceDescriptions: [
        { name: 'api', url: '/shop.json', type: 'openapi' },
        { name: 'payments', url: '/payments.arazzo.json', type: 'arazzo' },
      ],
      workflows: [checkout],
    } as unknown as ArazzoDocument

    // Act
    const sequence = buildSequence(owner, checkout, { shop, billing, payments }, { actorExtension: 'x-actor', actorsExtension: 'x-actors' })

    // Assert: each request is on its own service's lane, and only the parent's is titled by the registry.
    const lanes = Object.fromEntries(sequence.participants.map((participant) => [participant.id, participant]))
    const arrows = sequence.messages.map(({ stepId, kind, from, to, label }) => [stepId, kind, lanes[from]!.label, lanes[to]!.label, label])
    expect(arrows).toEqual([
      ['order', 'request', 'Client', 'Shop', 'GET /order'],
      ['order', 'response', 'Shop', 'Client', 'response'],
      ['charge', 'request', 'cashier', 'api', 'GET /charge'],
      ['charge', 'response', 'api', 'cashier', 'response'],
    ])
    const billingLane = sequence.participants.find((participant) => participant.detail === 'OpenAPI · Billing')
    expect(billingLane?.id).not.toBe('source:api')
    expect(sequence.participants.find((participant) => participant.id === 'source:api')?.detail).toBe('OpenAPI · Shop')
  })

  it('shares a lane when another document\'s source of the same name points at the same service', () => {
    // Arrange: both documents call the shop `api`.
    const shop = {
      openapi: '3.1.0',
      info: { title: 'Shop' },
      'x-scalar-original-source-url': '/shop.json',
      paths: { '/order': { get: { operationId: 'getOrder' } } },
    }
    const other = {
      arazzo: '1.1.0',
      'x-scalar-original-source-url': '/other.arazzo.json',
      sourceDescriptions: [{ name: 'api', url: '/shop.json', type: 'openapi' }],
      workflows: [{ workflowId: 'inner', steps: [{ stepId: 'again', operationPath: '{$sourceDescriptions.api.url}#/paths/~1order/get' }] }],
    }
    const outer = {
      workflowId: 'outer',
      steps: [
        { stepId: 'first', operationPath: '{$sourceDescriptions.api.url}#/paths/~1order/get' },
        { stepId: 'run', workflowId: '$sourceDescriptions.other.inner' },
      ],
    } as unknown as Workflow
    const owner: ArazzoDocument = {
      arazzo: '1.1.0',
      sourceDescriptions: [
        { name: 'api', url: '/shop.json', type: 'openapi' },
        { name: 'other', url: '/other.arazzo.json', type: 'arazzo' },
      ],
      workflows: [outer],
    }

    // Act
    const sequence = buildSequence(owner, outer, { shop, other })

    // Assert
    expect(sequence.participants.map((participant) => participant.id)).toEqual([CLIENT_ID, 'source:api'])
  })

  it('widens the diagram for a long call to itself on the rightmost lane, so its text is not cut off', () => {
    // Arrange
    const label = 'GET /orders/{orderId}/payment-status'
    const sequence = {
      participants: [{ id: CLIENT_ID, label: 'Client' }, { id: 'a', label: 'a' }],
      messages: [{ stepId: 's', from: 'a', to: 'a', label, kind: 'request' as const, note: 'retry after 2 s × 3, then give up and end the run' }],
      stages: [],
      parallels: [],
    }

    // Act
    const wrapper = mount(ArazzoSequenceDiagram, { props: { sequence, stepSectionIds: {} } })

    // Assert: the viewBox reaches past where the note's text ends, beyond the lanes' own width.
    const width = Number(wrapper.find('svg').attributes('viewBox')!.split(' ')[2])
    const start = Number(wrapper.find('.ba-sequence-label').attributes('x'))
    expect(width).toBeGreaterThan(120 + 2 * 210)
    expect(width).toBeGreaterThan(start + label.length * 6.5)
    wrapper.unmount()
  })
})

describe('steps that run at the same time', () => {
  // `create` first; `stock` and `payment` (two stages) both wait only on it; `confirm` waits on both. Inside
  // `confirmOrder`, `capture` and `commit` are independent and `announce` waits on both.
  const checkout: Workflow = {
    workflowId: 'checkout',
    steps: [
      { stepId: 'create', operationId: 'createOrder' },
      { stepId: 'stock', workflowId: 'reserveStock', dependsOn: ['create'] },
      { stepId: 'payment', workflowId: 'takePayment', dependsOn: ['create'] },
      { stepId: 'confirm', workflowId: 'confirmOrder', dependsOn: ['stock', 'payment'] },
    ],
  }
  const owner: ArazzoDocument = {
    ...document,
    workflows: [
      checkout,
      { workflowId: 'reserveStock', summary: 'Reserve the stock', steps: [{ stepId: 'reserve', operationId: 'createOrder' }] },
      { workflowId: 'takePayment', summary: 'Take the payment', steps: [{ stepId: 'authorize', operationId: 'createOrder' }] },
      {
        workflowId: 'confirmOrder',
        summary: 'Confirm the order',
        steps: [
          { stepId: 'capture', operationId: 'createOrder' },
          { stepId: 'commit', operationId: 'createOrder' },
          { stepId: 'announce', operationId: 'publishOrderPlaced', dependsOn: ['capture', 'commit'] },
        ],
      },
    ],
  }

  it('groups consecutive steps none of which waits on another, only when the workflow uses dependsOn', () => {
    // Arrange
    const ids = (groups: ArazzoStep[][]) => groups.map((group) => group.map((step) => step.stepId))
    const sequential: Workflow = { workflowId: 's', steps: [{ stepId: 'a' }, { stepId: 'b' }] }
    const reading: Workflow = {
      workflowId: 'r',
      steps: [
        { stepId: 'a', dependsOn: [] },
        { stepId: 'b', dependsOn: ['a'] },
        // Waits on `b` by reading its output, without listing it.
        { stepId: 'c', dependsOn: ['a'], parameters: [{ name: 'x', value: '$steps.b.outputs.x' }] },
      ],
    }

    // Act / Assert
    expect(ids(parallelGroups(owner, checkout, checkout.steps!))).toEqual([['create'], ['stock', 'payment'], ['confirm']])
    expect(ids(parallelGroups(owner, sequential, sequential.steps!))).toEqual([['a'], ['b']])
    expect(ids(parallelGroups(owner, reading, reading.steps!))).toEqual([['a'], ['b'], ['c']])
  })

  it('notes the parallel steps of the workflow and of each stage', () => {
    // Act
    const sequence = buildSequence(owner, checkout, documents)

    // Assert
    expect(sequence.parallels).toEqual([
      { id: '/stock', steps: ['stock', 'payment'] },
      { id: 'confirm/capture', scope: 'confirm', steps: ['capture', 'commit'] },
    ])
  })

  it('frames them in a par with a dashed line between branches, nesting stages and pars inside each other', () => {
    // Act
    const wrapper = mount(ArazzoSequenceDiagram, { props: { sequence: buildSequence(owner, checkout, documents), stepSectionIds: {} } })

    // Assert
    const frame = (element: DOMWrapper<Element>) => ({
      x: Number(element.attributes('x')),
      top: Number(element.attributes('y')),
      bottom: Number(element.attributes('y')) + Number(element.attributes('height')),
    })
    const [outer, inner] = wrapper.findAll('.ba-sequence-par > rect').map(frame)
    // Bands are drawn outermost first; find each by its title.
    const band = (title: string) => frame(wrapper.findAll('.ba-sequence-stage').find((stage) => stage.find('.ba-sequence-stage-title').text().endsWith(title))!.find('rect'))
    const [stock, payment, confirm] = ['Reserve the stock', 'Take the payment', 'Confirm the order'].map(band)
    expect(wrapper.findAll('.ba-sequence-par .ba-sequence-fragment-label').map((label) => label.text())).toEqual(['par', 'par'])
    // The two parallel stages sit inside the outer par, inset from it; the inner par sits inside `confirm`.
    expect(stock!.x).toBeGreaterThan(outer!.x)
    expect([stock, payment].every((band) => band!.top > outer!.top && band!.bottom < outer!.bottom)).toBe(true)
    expect(confirm!.x).toBe(outer!.x)
    expect(confirm!.top).toBeGreaterThan(outer!.bottom)
    expect(inner!.x).toBeGreaterThan(confirm!.x)
    expect(inner!.top > confirm!.top && inner!.bottom < confirm!.bottom).toBe(true)
    // One dashed line between the two branches of each par, between their rows.
    const separators = wrapper.findAll('.ba-sequence-par .ba-sequence-fragment-separator').map((line) => Number(line.attributes('y1')))
    expect(separators).toHaveLength(2)
    expect(separators[0]).toBeGreaterThan(stock!.bottom)
    expect(separators[0]).toBeLessThan(payment!.top)
    // The diagram is tall enough for every frame.
    expect(Number(wrapper.find('svg').attributes('viewBox')!.split(' ')[3])).toBeGreaterThan(Math.max(outer!.bottom, confirm!.bottom))
    wrapper.unmount()
  })
})

describe('the ways a step can end', () => {
  it('frames a step\'s responses in an alt, each operand under its condition, and leaves the request outside', () => {
    // Arrange
    const workflow: Workflow = {
      workflowId: 'w',
      steps: [
        {
          stepId: 'create',
          operationId: 'createOrder',
          successCriteria: [{ condition: '$statusCode == 201' }],
          onFailure: [
            { name: 'rejected', type: 'end', criteria: [{ condition: '$statusCode == 400' }] },
            { name: 'conflict', type: 'goto', stepId: 'create', criteria: [{ condition: '$statusCode == 409' }] },
          ],
        },
        { stepId: 'announce', operationId: 'publishOrderPlaced' },
      ],
    }

    // Act
    const wrapper = mount(ArazzoSequenceDiagram, { props: { sequence: buildSequence(document, workflow, documents), stepSectionIds: {} } })

    // Assert
    const alt = wrapper.find('.ba-sequence-alt')
    expect(alt.find('.ba-sequence-fragment-label').text()).toBe('alt')
    expect(alt.findAll('.ba-sequence-guard').map((guard) => guard.text())).toEqual(['[201]', '[400]', '[409]'])
    expect(alt.findAll('.ba-sequence-fragment-separator')).toHaveLength(2)
    const box = alt.find('rect')
    const [top, bottom] = [Number(box.attributes('y')), Number(box.attributes('y')) + Number(box.attributes('height'))]
    const rows = wrapper.findAll('.ba-sequence-line').map((line) => Number(line.attributes('y1')))
    // The request, then the three responses inside the alt, then the next step below it.
    expect(rows[0]).toBeLessThan(top)
    expect(rows.slice(1, 4).every((y) => y > top && y < bottom)).toBe(true)
    expect(rows[4]).toBeGreaterThan(bottom)
    // Each condition sits between its dashed line and its response.
    const separators = alt.findAll('.ba-sequence-fragment-separator').map((line) => Number(line.attributes('y1')))
    expect(separators[0]).toBeGreaterThan(rows[1]!)
    expect(separators[0]).toBeLessThan(rows[2]!)
    expect(wrapper.findAll('.ba-sequence-step text').map((step) => step.text())).toEqual(['create', 'announce'])
    wrapper.unmount()
  })
})
