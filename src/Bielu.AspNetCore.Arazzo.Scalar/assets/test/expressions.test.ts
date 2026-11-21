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
    expect(resolveExpression('$workflows.adopt.outputs.adoptionId', scope)).toMatchObject({ anchor: 'doc/workflow/adopt/outputs/adoptionId' })
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

  it('skips prose fields', () => {
    // Act / Assert
    expect(stepExpressions(document, { stepId: 'x', successCriteria: [{ condition: 'true', description: '$inputs.a' } as never] })).toEqual([])
  })
})

describe('output consumers', () => {
  it('lists the steps and workflow outputs that read each step output', () => {
    // Act
    const consumers = stepOutputConsumers(scope, 'getPet')

    // Assert
    expect(consumers.name).toEqual([
      { label: 'adoptPet', anchor: 'doc/workflow/adopt/step/adoptPet', stepSection: 'doc/workflow/adopt/step/adoptPet' },
      { label: 'workflow outputs', anchor: 'doc/workflow/adopt/outputs' },
    ])
    expect(consumers.status).toBeUndefined()
  })

  it('lists the steps of other workflows that read a workflow output', () => {
    // Act
    const consumers = workflowOutputConsumers(scope)

    // Assert
    expect(consumers.adoptionId).toEqual([
      { label: 'notify / send', anchor: 'doc/workflow/notify/step/send', stepSection: 'doc/workflow/notify/step/send' },
    ])
  })
})

describe('the rendered document', () => {
  let wrapper: VueWrapper | undefined

  afterEach(() => {
    wrapper?.unmount()
    wrapper = undefined
  })

  const render = async () => {
    wrapper = mount(ArazzoDocument, {
      attachTo: globalThis.document.body,
      props: {
        document,
        documentName: 'doc',
        documents: {},
        navigation: traverseArazzoDocument({ documentId: 'doc', document: document as unknown as Record<string, unknown> }),
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
})
