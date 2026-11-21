// @vitest-environment happy-dom
import { flushPromises, mount, type VueWrapper } from '@vue/test-utils'
import { afterEach, describe, expect, it } from 'vitest'
import ArazzoDocument from '../src/components/ArazzoDocument.vue'
import {
  expressionsIn,
  parseExpression,
  resolveExpression,
  splitExpressions,
  stepExpressions,
  stepOutputConsumers,
  workflowOutputConsumers,
  type ExpressionScope,
} from '../src/helpers/expressions'
import { traverseArazzoDocument } from '../src/helpers/navigation'
import type { ArazzoDocument as Document } from '../src/types'

const document: Document = {
  arazzo: '1.1.0',
  sourceDescriptions: [{ name: 'petStore', url: '/openapi.json', type: 'openapi' }],
  components: { parameters: { tenant: { name: 'X-Tenant', in: 'header', value: '$inputs.tenant' } } },
  workflows: [
    {
      workflowId: 'adopt',
      inputs: { type: 'object', properties: { petId: { type: 'string' }, tenant: { type: 'string' } } },
      steps: [
        {
          stepId: 'getPet',
          operationId: 'getPet',
          parameters: [{ name: 'id', in: 'path', value: '$inputs.petId' }, { reference: '$components.parameters.tenant' }],
          successCriteria: [{ condition: '$statusCode == 200' }],
          outputs: { name: '$response.body#/name', status: '$response.body#/status' },
        },
        {
          stepId: 'adoptPet',
          operationId: 'adoptPet',
          requestBody: { payload: { pet: '$steps.getPet.outputs.name', note: 'For {$inputs.owner}' } },
          successCriteria: [{ condition: '$steps.getPet.outputs.missing == 1' }],
          outputs: { adoptionId: '$response.body#/id' },
        },
      ],
      outputs: { adoptionId: '$steps.adoptPet.outputs.adoptionId', pet: '$steps.getPet.outputs.name' },
    },
    {
      workflowId: 'notify',
      steps: [{ stepId: 'send', operationId: 'notify', parameters: [{ name: 'id', value: '$workflows.adopt.outputs.adoptionId' }] }],
    },
  ],
}

const adopt = document.workflows![0]!
const scope: ExpressionScope & { workflow: typeof adopt } = {
  document,
  workflow: adopt,
  workflowNavIds: { adopt: 'doc/workflow/adopt', notify: 'doc/workflow/notify' },
  stepSectionIds: {
    adopt: { getPet: 'doc/workflow/adopt/step/getPet', adoptPet: 'doc/workflow/adopt/step/adoptPet' },
    notify: { send: 'doc/workflow/notify/step/send' },
  },
  componentsId: 'doc/components',
}

describe('splitExpressions', () => {
  it('finds expressions standing alone, in conditions and embedded in braces, and keeps the text whole', () => {
    // Arrange
    const text = 'Bearer {$inputs.token} when $statusCode == 200 && $response.body#/status == "ok".'

    // Act
    const segments = splitExpressions(text)

    // Assert
    expect(segments.map((segment) => segment.text).join('')).toBe(text)
    expect(segments.filter((segment) => segment.expression).map((segment) => segment.text)).toEqual([
      '$inputs.token',
      '$statusCode',
      '$response.body#/status',
    ])
  })

  it('leaves a JSONPath `$` and other dollar signs as text', () => {
    // Act / Assert
    expect(expressionsIn('$.items[0].id costs $5')).toEqual([])
  })

  it('does not take a sentence-ending period into the expression', () => {
    // Act / Assert
    expect(expressionsIn('Reads $steps.a.outputs.b.')).toEqual(['$steps.a.outputs.b'])
  })

  it('keeps a trailing period that is part of the name, in a whole value or in braces', () => {
    // Act / Assert
    expect(expressionsIn('$steps.create.outputs.order.id.')).toEqual(['$steps.create.outputs.order.id.'])
    expect(expressionsIn(' $steps.create.outputs.order.id. ')).toEqual(['$steps.create.outputs.order.id.'])
    expect(expressionsIn('Order {$steps.create.outputs.order.id.} placed.')).toEqual(['$steps.create.outputs.order.id.'])
  })
})

describe('parseExpression', () => {
  it('reads step, workflow, input, component and source references', () => {
    // Act / Assert
    expect(parseExpression('$steps.getPet.outputs.name')).toEqual({ source: 'steps', stepId: 'getPet', field: 'outputs', name: 'name' })
    expect(parseExpression('$workflows.adopt.outputs.id')).toEqual({ source: 'workflows', workflowId: 'adopt', field: 'outputs', name: 'id' })
    expect(parseExpression('$inputs.petId#/0')).toEqual({ source: 'inputs', name: 'petId' })
    expect(parseExpression('$components.parameters.tenant')).toEqual({ source: 'components', kind: 'parameters', name: 'tenant' })
    expect(parseExpression('$sourceDescriptions.petStore.getPet')).toEqual({ source: 'sourceDescriptions', name: 'petStore' })
    expect(parseExpression('$response.body#/id')).toEqual({ source: 'other' })
    expect(parseExpression('$steps')).toEqual({ source: 'other' })
  })
})

describe('resolveExpression', () => {
  it('links inputs, step outputs, workflow outputs and components to their rows', () => {
    // Act / Assert
    expect(resolveExpression('$inputs.petId', scope)).toMatchObject({ anchor: 'doc/workflow/adopt/inputs/petId' })
    expect(resolveExpression('$steps.getPet.outputs.name', scope)).toMatchObject({
      anchor: 'doc/workflow/adopt/step/getPet/outputs/name',
      stepSection: 'doc/workflow/adopt/step/getPet',
    })
    expect(resolveExpression('$steps.getPet', scope)).toMatchObject({ anchor: 'doc/workflow/adopt/step/getPet' })
    expect(resolveExpression('$workflows.adopt.outputs.adoptionId', scope)).toMatchObject({
      anchor: 'doc/workflow/adopt/outputs/adoptionId',
      workflowSection: 'doc/workflow/adopt',
    })
    expect(resolveExpression('$components.parameters.tenant', scope)).toMatchObject({ anchor: 'doc/components/parameters/tenant' })
  })

  it('marks references to things the document does not declare as broken, saying why', () => {
    // Act / Assert
    expect(resolveExpression('$inputs.owner', scope).broken).toBe('Workflow adopt has no input “owner”')
    expect(resolveExpression('$steps.missing.outputs.x', scope).broken).toBe('Workflow adopt has no step “missing”')
    expect(resolveExpression('$steps.getPet.outputs.missing', scope).broken).toBe('Step getPet has no output “missing”')
    expect(resolveExpression('$workflows.nope.outputs.x', scope).broken).toBe('This document has no workflow “nope”')
    expect(resolveExpression('$workflows.adopt.outputs.nope', scope).broken).toBe('Workflow adopt has no output “nope”')
    expect(resolveExpression('$components.parameters.nope', scope).broken).toBe('This document has no parameters component “nope”')
    expect(resolveExpression('$sourceDescriptions.nope.x', scope).broken).toBe('This document has no source description “nope”')
    expect(resolveExpression('$inputs.x', { ...scope, workflow: document.workflows![1] }).broken).toBe('Workflow notify declares no inputs')
  })

  it('leaves expressions over the exchange unlinked but not broken', () => {
    // Act
    const target = resolveExpression('$response.body#/id', scope)

    // Assert
    expect(target.anchor).toBeUndefined()
    expect(target.broken).toBeUndefined()
  })

  it('cannot check workflow-local references outside a workflow', () => {
    // Act / Assert
    expect(resolveExpression('$steps.a.outputs.b', { document })).toEqual({ title: 'Step a' })
    expect(resolveExpression('$inputs.a', { document })).toEqual({ title: 'Input a' })
  })

  it('does not reject inputs when the schema lists no properties', () => {
    // Act
    const target = resolveExpression('$inputs.anything', { ...scope, workflow: { ...adopt, inputs: { $ref: '#/components/inputs/missing' } } })

    // Assert
    expect(target.broken).toBeUndefined()
    expect(target.anchor).toBe('doc/workflow/adopt/inputs')
  })
})

describe('stepExpressions', () => {
  it('reads parameters with their components followed, the body, criteria and outputs', () => {
    // Act
    const expressions = stepExpressions(document, adopt.steps![0]!)

    // Assert
    expect(expressions).toEqual([
      '$inputs.petId',
      '$components.parameters.tenant',
      '$inputs.tenant',
      '$statusCode',
      '$response.body#/name',
      '$response.body#/status',
    ])
  })

  it('reads the value a received message is matched on', () => {
    // Act / Assert
    expect(stepExpressions(document, { stepId: 'wait', correlationId: '$steps.getPet.outputs.name' })).toEqual(['$steps.getPet.outputs.name'])
  })

  it('skips prose fields', () => {
    // Act / Assert
    expect(stepExpressions(document, { stepId: 'x', successCriteria: [{ condition: 'true', description: '$inputs.a' } as never] })).toEqual([])
  })

  it('reads fields named like prose inside payloads, parameter values and outputs', () => {
    // Arrange
    const step = {
      stepId: 'x',
      parameters: [{ name: 'filter', in: 'query' as const, value: { summary: '$inputs.summary' } }],
      requestBody: { payload: { title: '{$steps.create.outputs.itemName}', nested: { description: '$inputs.text' } } },
      outputs: { title: '$response.body#/title' },
    }

    // Act
    const expressions = stepExpressions(document, step as never)

    // Assert
    expect(expressions).toEqual(['$inputs.summary', '$steps.create.outputs.itemName', '$inputs.text', '$response.body#/title'])
  })

  it('lists a step whose payload reads an output in a `title` field as its consumer', () => {
    // Arrange
    const workflow = {
      workflowId: 'w',
      steps: [
        { stepId: 'create', operationId: 'create', outputs: { itemName: '$response.body#/name' } },
        { stepId: 'rename', operationId: 'rename', requestBody: { payload: { title: '{$steps.create.outputs.itemName}' } } },
      ],
    }

    // Act
    const consumers = stepOutputConsumers({ document: { ...document, workflows: [workflow] }, workflow }, 'create')

    // Assert
    expect(consumers.itemName?.map((consumer) => consumer.label)).toEqual(['rename'])
  })

  it('follows the parameter components an action passes, inline or reusable, keeping their references', () => {
    // Arrange
    const withActions: Document = {
      ...document,
      components: {
        parameters: { orderId: { name: 'id', value: '$steps.create.outputs.id' } },
        failureActions: { retryOrder: { name: 'retryOrder', type: 'goto', workflowId: 'retry', parameters: [{ reference: '$components.parameters.orderId' }] } },
      },
    }
    const step = {
      stepId: 'x',
      onSuccess: [{ name: 'next', type: 'goto' as const, workflowId: 'next', parameters: [{ reference: '$components.parameters.orderId' }] }],
      onFailure: [{ reference: '$components.failureActions.retryOrder' }],
    }

    // Act
    const expressions = stepExpressions(withActions, step as never)

    // Assert
    expect(expressions).toEqual([
      '$components.parameters.orderId',
      '$steps.create.outputs.id',
      '$components.failureActions.retryOrder',
      '$components.parameters.orderId',
      '$steps.create.outputs.id',
    ])
  })
})

describe('dotted names', () => {
  const dotted: Document = {
    arazzo: '1.1.0',
    sourceDescriptions: [],
    components: { parameters: { 'tenant.header': { name: 'X-Tenant', in: 'header', value: 'a' }, tenant: { name: 'T', in: 'header', value: 'b' } } },
    workflows: [
      {
        workflowId: 'w',
        inputs: { type: 'object', properties: { 'order.id': { type: 'string' }, order: { type: 'object' } } },
        steps: [
          { stepId: 'create', operationId: 'create', outputs: { 'order.id': '$response.body#/id', order: '$response.body' } },
          { stepId: 'use', operationId: 'use', parameters: [{ name: 'id', in: 'path', value: '$steps.create.outputs.order.id' }] },
        ],
        outputs: { 'order.id': '$steps.create.outputs.order.id' },
      },
    ],
  }
  const dottedScope: ExpressionScope & { workflow: NonNullable<Document['workflows']>[number] } = {
    document: dotted,
    workflow: dotted.workflows![0]!,
    workflowNavIds: { w: 'doc/workflow/w' },
    stepSectionIds: { w: { create: 'doc/workflow/w/step/create', use: 'doc/workflow/w/step/use' } },
    componentsId: 'doc/components',
  }

  it('keeps the whole name after the fixed prefix, up to a JSON Pointer', () => {
    // Act / Assert
    expect(parseExpression('$inputs.order.id')).toEqual({ source: 'inputs', name: 'order.id' })
    expect(parseExpression('$outputs.order.id#/0')).toEqual({ source: 'outputs', name: 'order.id' })
    expect(parseExpression('$steps.create.outputs.order.id')).toEqual({ source: 'steps', stepId: 'create', field: 'outputs', name: 'order.id' })
    expect(parseExpression('$workflows.w.outputs.order.id#/x.y')).toEqual({ source: 'workflows', workflowId: 'w', field: 'outputs', name: 'order.id' })
    expect(parseExpression('$components.parameters.tenant.header')).toEqual({ source: 'components', kind: 'parameters', name: 'tenant.header' })
  })

  it('links dotted inputs, step outputs, workflow outputs and component keys to their own rows', () => {
    // Act / Assert
    expect(resolveExpression('$inputs.order.id', dottedScope)).toMatchObject({ anchor: 'doc/workflow/w/inputs/order.id' })
    expect(resolveExpression('$steps.create.outputs.order.id', dottedScope)).toMatchObject({ anchor: 'doc/workflow/w/step/create/outputs/order.id' })
    expect(resolveExpression('$workflows.w.outputs.order.id', dottedScope)).toMatchObject({ anchor: 'doc/workflow/w/outputs/order.id' })
    expect(resolveExpression('$components.parameters.tenant.header', dottedScope)).toMatchObject({ anchor: 'doc/components/parameters/tenant.header' })
    expect(resolveExpression('$steps.create.outputs.order.id', dottedScope).broken).toBeUndefined()
  })

  it('counts a dotted output as read under its full name', () => {
    // Act
    const consumers = stepOutputConsumers(dottedScope, 'create')

    // Assert
    expect(consumers['order.id']?.map((consumer) => consumer.label)).toEqual(['use', 'workflow outputs'])
    expect(consumers.order).toBeUndefined()
  })
})

describe('output consumers', () => {
  it('lists the steps and workflow outputs that read each step output', () => {
    // Act
    const consumers = stepOutputConsumers(scope, 'getPet')

    // Assert
    expect(consumers.name).toEqual([
      { label: 'adoptPet', anchor: 'doc/workflow/adopt/step/adoptPet', stepSection: 'doc/workflow/adopt/step/adoptPet' },
      { label: 'workflow outputs', anchor: 'doc/workflow/adopt/outputs', workflowSection: 'doc/workflow/adopt' },
    ])
    expect(consumers.status).toBeUndefined()
  })

  it('links workflow parameters and actions that read a step output to the workflow, not to an outputs block', () => {
    // Arrange
    const workflow = {
      workflowId: 'w',
      steps: [{ stepId: 'create', operationId: 'create', outputs: { id: '$response.body#/id' } }],
      parameters: [{ name: 'id', value: '$steps.create.outputs.id' }],
      successActions: [{ name: 'done', type: 'end' as const, criteria: [{ condition: '$steps.create.outputs.id != null' }] }],
    }
    const actionScope = { document: { ...document, workflows: [workflow] }, workflow, workflowNavIds: { w: 'doc/workflow/w' } }

    // Act
    const consumers = stepOutputConsumers(actionScope, 'create')

    // Assert
    expect(consumers.id).toEqual([
      { label: 'workflow parameters', anchor: 'doc/workflow/w', workflowSection: 'doc/workflow/w' },
      { label: 'workflow actions', anchor: 'doc/workflow/w', workflowSection: 'doc/workflow/w' },
    ])
  })

  it('lists the outputs of other workflows that read a workflow output', () => {
    // Arrange
    const reader = { workflowId: 'report', steps: [{ stepId: 'noop' }], outputs: { adoption: '$workflows.adopt.outputs.adoptionId' } }
    const readerScope = { ...scope, document: { ...document, workflows: [...document.workflows!, reader] }, workflowNavIds: { ...scope.workflowNavIds, report: 'doc/workflow/report' } }

    // Act
    const consumers = workflowOutputConsumers(readerScope)

    // Assert
    expect(consumers.adoptionId?.map((consumer) => consumer.label)).toEqual(['notify / send', 'report / outputs'])
    expect(consumers.adoptionId?.[1]).toEqual({ label: 'report / outputs', anchor: 'doc/workflow/report/outputs', workflowSection: 'doc/workflow/report' })
  })

  it('lists the steps of other workflows that read a workflow output', () => {
    // Act
    const consumers = workflowOutputConsumers(scope)

    // Assert
    expect(consumers.adoptionId).toEqual([
      { label: 'notify / send', anchor: 'doc/workflow/notify/step/send', stepSection: 'doc/workflow/notify/step/send' },
    ])
  })

  it('handles outputs named like Object.prototype members in both scopes', () => {
    // Arrange
    const named: Document = {
      arazzo: '1.1.0',
      sourceDescriptions: [],
      workflows: [
        {
          workflowId: 'make',
          steps: [
            { stepId: 'create', operationId: 'create', outputs: { constructor: '$response.body#/id' } },
            { stepId: 'read', operationId: 'read', parameters: [{ name: 'id', in: 'path', value: '$steps.create.outputs.constructor' }] },
          ],
          outputs: { toString: '$steps.create.outputs.constructor' },
        },
        { workflowId: 'other', steps: [{ stepId: 'call', workflowId: 'make', parameters: [{ name: 'v', value: '$workflows.make.outputs.toString' }] }] },
      ],
    }
    const namedScope = { document: named, workflow: named.workflows![0]! }

    // Act
    const stepConsumers = stepOutputConsumers(namedScope, 'create')
    const workflowConsumers = workflowOutputConsumers(namedScope)

    // Assert
    expect(stepConsumers.constructor?.map((consumer) => consumer.label)).toEqual(['read', 'workflow outputs'])
    expect(workflowConsumers.toString?.map((consumer) => consumer.label)).toEqual(['other / call'])
    expect(workflowConsumers.constructor).toBeUndefined()
  })
})

describe('the rendered document', () => {
  let wrapper: VueWrapper | undefined

  afterEach(() => {
    wrapper?.unmount()
    wrapper = undefined
  })

  const render = async (rendered: Document = document) => {
    wrapper = mount(ArazzoDocument, {
      attachTo: globalThis.document.body,
      props: {
        document: rendered,
        documentName: 'doc',
        documents: {},
        navigation: traverseArazzoDocument({ documentId: 'doc', document: rendered as unknown as Record<string, unknown> }),
      },
    })
    await flushPromises()
    return wrapper
  }

  it('links expressions to the rows they read and marks broken ones', async () => {
    // Act
    const view = await render()

    // Assert
    const links = view.findAll('a.ba-expression-link').map((link) => link.attributes('href'))
    expect(links).toContain('#doc/workflow/adopt/inputs/petId')
    expect(links).toContain('#doc/workflow/adopt/step/getPet/outputs/name')
    expect(links).toContain('#doc/components/parameters/tenant')
    const broken = view.findAll('.ba-expression-broken').map((element) => element.text())
    expect(broken).toEqual(expect.arrayContaining(['$inputs.owner', '$steps.getPet.outputs.missing']))
    expect(view.find('[id="doc/workflow/adopt/inputs/petId"]').exists()).toBe(true)
    expect(view.find('[id="doc/workflow/adopt/step/getPet/outputs/name"]').exists()).toBe(true)
    expect(view.find('[id="doc/components/parameters/tenant"]').exists()).toBe(true)
  })

  it('shows which steps read each output', async () => {
    // Act
    const view = await render()

    // Assert
    const usedBy = view.find('[id="doc/workflow/adopt/step/getPet"] .ba-used-by')
    expect(usedBy.text()).toBe('Used by adoptPet, workflow outputs')
    expect(view.find('[id="doc/workflow/adopt/outputs"] .ba-used-by').text()).toBe('Used by notify / send')
  })

  it('opens the step an expression points into and highlights the row', async () => {
    // Arrange
    const view = await render()
    await view.find('[id="doc/workflow/adopt/step/getPet"] .ba-step-toggle').trigger('click')
    expect(view.find('[id="doc/workflow/adopt/step/getPet"]').classes()).toContain('ba-step-closed')
    const link = view.findAll('a.ba-expression-link').find((element) => element.attributes('href') === '#doc/workflow/adopt/step/getPet/outputs/name')!

    // Act
    await link.trigger('click', { button: 0 })
    await flushPromises()

    // Assert
    expect(view.find('[id="doc/workflow/adopt/step/getPet"]').classes()).not.toContain('ba-step-closed')
    const row = view.find('[id="doc/workflow/adopt/step/getPet/outputs/name"]')
    expect(row.classes()).toContain('ba-flash')
    expect((row.element.nextElementSibling as HTMLElement).classList).toContain('ba-flash')
  })

  it('shows the workflow a link to its output lands in while the filter hides it', async () => {
    // Arrange
    const view = await render()
    await view.find('.ba-filter-input').setValue('notify')
    await flushPromises()
    const workflow = view.find('[id="doc/workflow/adopt"]').element as HTMLElement
    expect(workflow.style.display).toBe('none')
    const link = view.findAll('a.ba-expression-link').find((element) => element.attributes('href') === '#doc/workflow/adopt/outputs/adoptionId')!

    // Act
    await link.trigger('click', { button: 0 })
    await flushPromises()

    // Assert
    expect(workflow.style.display).toBe('')
    expect(view.find('[id="doc/workflow/adopt/outputs/adoptionId"]').classes()).toContain('ba-flash')
  })

  it('anchors a component action by its key in the components map, not by its name', async () => {
    // Arrange
    const keyed: Document = {
      ...document,
      components: { ...document.components, failureActions: { refreshToken: { name: 'refreshExpiredToken', type: 'retry' } } },
    }

    // Act
    const view = await render(keyed)

    // Assert
    const action = view.find('[id="doc/components/failureActions/refreshToken"]')
    expect(action.exists()).toBe(true)
    expect(action.find('.ba-action-name').text()).toBe('refreshExpiredToken')
  })
})
