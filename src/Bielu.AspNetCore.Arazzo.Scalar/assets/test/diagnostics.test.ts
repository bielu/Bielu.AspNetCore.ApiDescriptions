// @vitest-environment happy-dom
import { flushPromises, mount, type VueWrapper } from '@vue/test-utils'
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'
import ArazzoDocument from '../src/components/ArazzoDocument.vue'
import { diagnose, diagnosticSummary, type Diagnostic } from '../src/helpers/diagnostics'
import { traverseArazzoDocument } from '../src/helpers/navigation'
import type { ArazzoDocument as Document, ArazzoWorkflow } from '../src/types'

const petsApi = {
  openapi: '3.1.0',
  'x-scalar-original-source-url': '/openapi.json',
  paths: {
    '/pets/{id}': {
      parameters: [{ name: 'id', in: 'path', required: true, schema: { type: 'string' } }],
      get: {
        operationId: 'getPet',
        parameters: [
          { name: 'expand', in: 'query', schema: { type: 'string' } },
          { name: 'tenant', in: 'query', required: true, schema: { type: 'string' } },
        ],
        responses: { '200': { description: 'OK' }, '4XX': { description: 'Client error' } },
      },
    },
    '/pets': { post: { operationId: 'createPet', responses: { default: { description: 'Any' } } } },
  },
}

const documents = { pets: petsApi }

const documentWith = (workflow: ArazzoWorkflow, extra: Partial<Document> = {}): Document => ({
  arazzo: '1.1.0',
  sourceDescriptions: [{ name: 'pets', url: '/openapi.json', type: 'openapi' }],
  workflows: [workflow],
  ...extra,
})

const messages = (diagnostics: Diagnostic[], stepId?: string) =>
  diagnostics.filter((diagnostic) => diagnostic.stepId === stepId).map((diagnostic) => `${diagnostic.severity}: ${diagnostic.message}`)

beforeEach(() => {
  vi.stubGlobal('location', { href: 'https://app.test/scalar/' })
})

afterEach(() => {
  vi.unstubAllGlobals()
})

describe('diagnose', () => {
  it('reports nothing for a sound workflow', () => {
    // Arrange
    const document = documentWith({
      workflowId: 'adopt',
      inputs: { type: 'object', properties: { id: { type: 'string' } } },
      steps: [
        {
          stepId: 'get',
          operationId: 'getPet',
          parameters: [
            { name: 'id', in: 'path', value: '$inputs.id' },
            { name: 'tenant', in: 'query', value: 'acme' },
            { name: 'X-Trace', in: 'header', value: 'abc' },
          ],
          successCriteria: [{ condition: '$statusCode == 200' }],
          onFailure: [{ name: 'gone', type: 'end', criteria: [{ condition: '$statusCode == 404' }] }],
          outputs: { name: '$response.body#/name' },
        },
      ],
      outputs: { name: '$steps.get.outputs.name' },
    })

    // Act / Assert
    expect(diagnose(document, { documents })).toEqual([])
  })

  it('reports targets the loaded sources do not declare, and sources that do not exist', () => {
    // Arrange
    const document = documentWith({
      workflowId: 'w',
      steps: [
        { stepId: 'a', operationId: 'deletePet' },
        { stepId: 'b', operationPath: '{$sourceDescriptions.pets.url}#/paths/~1owners/get' },
        { stepId: 'c', operationId: '$sourceDescriptions.vets.listVets' },
        { stepId: 'd', workflowId: 'nope' },
      ],
    })

    // Act
    const diagnostics = diagnose(document, { documents })

    // Assert
    expect(messages(diagnostics, 'a')).toEqual(["error: Targets deletePet, which the loaded source descriptions don't declare"])
    expect(messages(diagnostics, 'b')).toEqual([
      "error: Targets {$sourceDescriptions.pets.url}#/paths/~1owners/get, which the loaded source “pets” don't declare",
    ])
    expect(messages(diagnostics, 'c')).toEqual(['error: Targets $sourceDescriptions.vets.listVets, but there is no source description “vets”'])
    expect(messages(diagnostics, 'd')).toEqual(["error: Invokes workflow “nope”, which this document doesn't declare"])
  })

  it('does not report a target whose source is not loaded', () => {
    // Arrange
    const document = documentWith({ workflowId: 'w', steps: [{ stepId: 'a', operationId: 'deletePet' }] })

    // Act / Assert
    expect(diagnose(document, { documents: {} })).toEqual([])
  })

  it('reports parameters the operation does not take, and required ones the step does not pass', () => {
    // Arrange
    const document = documentWith({
      workflowId: 'w',
      steps: [{ stepId: 'get', operationId: 'getPet', parameters: [{ name: 'limit', in: 'query', value: 5 }] }],
    })

    // Act
    const diagnostics = diagnose(document, { documents })

    // Assert
    expect(messages(diagnostics, 'get')).toEqual([
      'error: Doesn\'t pass the required path parameter “id”',
      "warning: Passes query parameter “limit”, which the operation doesn't declare",
      'warning: Doesn\'t pass the required query parameter “tenant”',
    ])
  })

  it('counts workflow parameters as passed to every step', () => {
    // Arrange
    const document = documentWith({
      workflowId: 'w',
      parameters: [
        { name: 'id', in: 'path', value: 'x' },
        { name: 'tenant', in: 'query', value: 'acme' },
      ],
      steps: [{ stepId: 'get', operationId: 'getPet' }],
    })

    // Act / Assert
    expect(diagnose(document, { documents })).toEqual([])
  })

  it('reports statuses the criteria check that the operation does not declare, unless a range or default covers them', () => {
    // Arrange
    const document = documentWith({
      workflowId: 'w',
      parameters: [
        { name: 'id', in: 'path', value: 'x' },
        { name: 'tenant', in: 'query', value: 'acme' },
      ],
      steps: [
        {
          stepId: 'get',
          operationId: 'getPet',
          successCriteria: [{ condition: '$statusCode == 201' }],
          onFailure: [
            { name: 'missing', type: 'end', criteria: [{ condition: '$statusCode == 404' }] },
            { name: 'broken', type: 'end', criteria: [{ condition: '$statusCode == 503' }] },
          ],
        },
        { stepId: 'create', operationId: 'createPet', successCriteria: [{ condition: '$statusCode == 201' }] },
      ],
    })

    // Act
    const diagnostics = diagnose(document, { documents })

    // Assert
    expect(messages(diagnostics, 'get')).toEqual([
      'warning: Its success criteria check for status 201, but the operation only declares 200, 4XX',
      'warning: Action broken checks for status 503, but the operation only declares 200, 4XX',
    ])
    expect(messages(diagnostics, 'create')).toEqual([])
  })

  it('reports actions and dependencies that lead nowhere, and steps that can never run', () => {
    // Arrange
    const document = documentWith({
      workflowId: 'w',
      steps: [
        { stepId: 'a', onSuccess: [{ name: 'done', type: 'end' }], onFailure: [{ name: 'again', type: 'goto', stepId: 'zz' }] },
        { stepId: 'b', dependsOn: ['a', 'ghost'], onFailure: [{ name: 'other', type: 'goto', workflowId: 'missing' }] },
      ],
    })

    // Act
    const diagnostics = diagnose(document, { documents: {} })

    // Assert
    expect(messages(diagnostics, 'a')).toEqual(["error: Action again goes to step “zz”, which this workflow doesn't have"])
    expect(messages(diagnostics, 'b')).toEqual([
      "error: Action other goes to workflow “missing”, which this document doesn't declare",
      "warning: Depends on a, but a's success action done always ends the workflow, so this step never runs",
      "error: Depends on step “ghost”, which this workflow doesn't have",
    ])
  })

  it('counts an inherited workflow success action that always ends the workflow', () => {
    // Arrange
    const document = documentWith({
      workflowId: 'w',
      successActions: [{ name: 'notify', type: 'end' }],
      steps: [{ stepId: 'a' }, { stepId: 'b', dependsOn: ['a'] }],
    })

    // Act / Assert
    expect(messages(diagnose(document, { documents: {} }), 'b')).toEqual([
      "warning: Depends on a, but a's success action notify always ends the workflow, so this step never runs",
    ])
  })

  it('reports broken expressions once, and outputs read before their step runs', () => {
    // Arrange
    const document = documentWith({
      workflowId: 'w',
      steps: [
        { stepId: 'a', parameters: [{ name: 'x', value: '$steps.b.outputs.id' }, { name: 'y', value: '$inputs.nope' }, { name: 'z', value: '$inputs.nope' }] },
        {
          stepId: 'b',
          parameters: [{ name: 'echo', value: '$steps.b.outputs.id' }],
          outputs: { id: '$response.body#/id' },
          // Actions run after the outputs are set: reading them there is fine.
          onFailure: [{ name: 'retryWith', type: 'goto', stepId: 'a', parameters: [{ name: 'id', value: '$steps.b.outputs.id' }] } as never],
        },
      ],
      outputs: { missing: '$steps.c.outputs.x' },
    })

    // Act
    const diagnostics = diagnose(document, { documents: {} })

    // Assert
    expect(messages(diagnostics, 'a')).toEqual([
      "warning: $steps.b.outputs.id reads step b, which doesn't run before this one",
      'error: $inputs.nope: Workflow w declares no inputs',
    ])
    expect(messages(diagnostics, 'b')).toEqual(["warning: $steps.b.outputs.id sends this step's own output, which only exists once it has run"])
    expect(messages(diagnostics)).toEqual(['error: $steps.c.outputs.x: Workflow w has no step “c”'])
  })

  it('follows dependsOn, transitively, for what runs before a step', () => {
    // Arrange
    const document = documentWith({
      workflowId: 'w',
      steps: [
        { stepId: 'c', dependsOn: ['b'], parameters: [{ name: 'x', value: '$steps.a.outputs.id' }] },
        { stepId: 'a', outputs: { id: '$response.body#/id' } },
        { stepId: 'b', dependsOn: ['a'] },
      ],
    })

    // Act / Assert
    expect(diagnose(document, { documents: {} })).toEqual([])
  })

  it('reports duplicate step ids, and anchors each problem to its section', () => {
    // Arrange
    const document = documentWith({ workflowId: 'w', steps: [{ stepId: 'a' }, { stepId: 'a' }] })

    // Act
    const diagnostics = diagnose(document, { documents: {}, stepSectionIds: { w: { a: 'doc/workflow/w/step/a' } } })

    // Assert
    expect(diagnostics).toEqual([
      { severity: 'error', message: 'Step id “a” is used by more than one step', workflowId: 'w', stepId: 'a', anchor: 'doc/workflow/w/step/a' },
    ])
  })
})

describe('diagnose across workflows and documents', () => {
  const partner = {
    arazzo: '1.1.0',
    'x-scalar-original-source-url': '/partner.arazzo.json',
    info: { title: 'Partner', version: '1' },
    sourceDescriptions: [],
    workflows: [{ workflowId: 'onboard', steps: [{ stepId: 'seed' }] }],
  }
  const crossDocument = (steps: ArazzoWorkflow['steps']): Document => ({
    arazzo: '1.1.0',
    sourceDescriptions: [{ name: 'partner', url: '/partner.arazzo.json', type: 'arazzo' }],
    workflows: [
      { workflowId: 'bootstrap', steps: [{ stepId: 'seed' }] },
      { workflowId: 'main', dependsOn: ['bootstrap'], steps },
    ],
  })

  it('accepts step dependencies on steps of other workflows that exist', () => {
    // Arrange
    const document = crossDocument([
      { stepId: 'a', dependsOn: ['$workflows.bootstrap.steps.seed', '$sourceDescriptions.partner.onboard.steps.seed'] },
    ])

    // Act / Assert
    expect(diagnose(document, { documents: { partner } })).toEqual([])
  })

  it('reports step dependencies on workflows or steps that do not exist', () => {
    // Arrange
    const document = crossDocument([
      {
        stepId: 'a',
        dependsOn: [
          '$workflows.bootstrap.steps.ghost',
          '$workflows.nope.steps.seed',
          '$sourceDescriptions.partner.onboard.steps.ghost',
          '$sourceDescriptions.partner.nope.steps.seed',
          '$sourceDescriptions.other.onboard.steps.seed',
        ],
      },
    ])

    // Act
    const diagnostics = diagnose(document, { documents: { partner } })

    // Assert
    expect(messages(diagnostics, 'a')).toEqual([
      'error: Depends on $workflows.bootstrap.steps.ghost, but workflow bootstrap has no step “ghost”',
      'error: Depends on $workflows.nope.steps.seed, but this document doesn\'t declare workflow “nope”',
      'error: Depends on $sourceDescriptions.partner.onboard.steps.ghost, but workflow onboard of source “partner” has no step “ghost”',
      'error: Depends on $sourceDescriptions.partner.nope.steps.seed, but the loaded source “partner” doesn\'t declare workflow “nope”',
      'error: Depends on $sourceDescriptions.other.onboard.steps.seed, but there is no source description “other”',
    ])
  })

  it('does not check dependencies on another document until it is loaded', () => {
    // Arrange
    const document = crossDocument([{ stepId: 'a', dependsOn: ['$sourceDescriptions.partner.nope.steps.seed'] }])

    // Act / Assert
    expect(diagnose(document, { documents: {} })).toEqual([])
  })

  it('reports a workflow a loaded Arazzo source does not declare, and nothing while it is loading', () => {
    // Arrange
    const document = crossDocument([
      { stepId: 'call', workflowId: '$sourceDescriptions.partner.missing' },
      { stepId: 'ok', workflowId: '$sourceDescriptions.partner.onboard' },
      { stepId: 'then', workflowId: 'bootstrap', onSuccess: [{ name: 'jump', type: 'goto', workflowId: '$sourceDescriptions.partner.gone' }] },
    ])

    // Act
    const loaded = diagnose(document, { documents: { partner } })
    const loading = diagnose(document, { documents: {} })

    // Assert
    expect(messages(loaded, 'call')).toEqual(['error: Invokes workflow $sourceDescriptions.partner.missing, which the loaded source “partner” doesn\'t declare'])
    expect(messages(loaded, 'ok')).toEqual([])
    expect(messages(loaded, 'then')).toEqual(['error: Action jump goes to workflow $sourceDescriptions.partner.gone, which the loaded source “partner” doesn\'t declare'])
    expect(loading).toEqual([])
  })
})

describe('diagnosticSummary', () => {
  it('counts errors and warnings', () => {
    // Arrange
    const of = (severity: Diagnostic['severity']): Diagnostic => ({ severity, message: '', workflowId: 'w' })

    // Act / Assert
    expect(diagnosticSummary([])).toBeUndefined()
    expect(diagnosticSummary([of('error')])).toBe('1 error')
    expect(diagnosticSummary([of('error'), of('warning'), of('warning')])).toBe('1 error · 2 warnings')
  })
})

describe('the rendered document', () => {
  let wrapper: VueWrapper | undefined

  afterEach(() => {
    wrapper?.unmount()
    wrapper = undefined
  })

  it('lists the problems in the header, on each step and on its box in the overview', async () => {
    // Arrange
    const document = documentWith({
      workflowId: 'w',
      steps: [
        { stepId: 'a', onSuccess: [{ name: 'done', type: 'end' }] },
        { stepId: 'b', dependsOn: ['a'], parameters: [{ name: 'x', value: '$inputs.nope' }] },
      ],
    })

    // Act
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

    // Assert
    expect(wrapper.find('.ba-diagnostics summary').text()).toContain('1 error · 1 warning found in this document')
    const listed = wrapper.findAll('.ba-diagnostic-list li')
    expect(listed[0]!.classes()).toContain('ba-diagnostic-error')
    expect(listed[0]!.find('a').attributes('href')).toBe('#doc/workflow/w/step/b')
    const step = wrapper.find('[id="doc/workflow/w/step/b"]')
    expect(step.find('.ba-step-problems').text()).toBe('✕ 2')
    expect(step.findAll('.ba-step-diagnostics li')).toHaveLength(2)
    expect(wrapper.find('[id="doc/workflow/w/step/a"] .ba-step-problems').exists()).toBe(false)
    expect(wrapper.find('.ba-overview [data-node-id="b"] .ba-diagram-problem').classes()).toContain('ba-diagnostic-error')
    expect(wrapper.find('.ba-overview [data-node-id="a"] .ba-diagram-problem').exists()).toBe(false)
  })

  it('shows no header entry for a document without problems', async () => {
    // Arrange
    const document = documentWith({ workflowId: 'w', steps: [{ stepId: 'a' }] })

    // Act
    wrapper = mount(ArazzoDocument, {
      props: {
        document,
        documentName: 'doc',
        documents: {},
        navigation: traverseArazzoDocument({ documentId: 'doc', document: document as unknown as Record<string, unknown> }),
      },
    })
    await flushPromises()

    // Assert
    expect(wrapper.find('.ba-diagnostics').exists()).toBe(false)
  })
})
