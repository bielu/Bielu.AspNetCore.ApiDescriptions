// @vitest-environment happy-dom
import { Badge } from '@scalar/blocks/shared'
import { flushPromises, mount, type VueWrapper } from '@vue/test-utils'
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'
import ArazzoDocument from '../src/components/ArazzoDocument.vue'
import { traverseArazzoDocument } from '../src/helpers/navigation'
import { operationRequestBody, operationResponses, responseHandling, statusCodesIn } from '../src/helpers/openapi-exchange'
import { resolveStepTarget } from '../src/helpers/resolve-target'
import type { ArazzoDocument as Document, ArazzoStep } from '../src/types'

const petsApi = {
  openapi: '3.1.0',
  'x-scalar-original-source-url': '/openapi.json',
  paths: {
    '/pets': {
      post: {
        operationId: 'createPet',
        requestBody: { $ref: '#/components/requestBodies/newPet' },
        responses: {
          default: { description: 'Unexpected error' },
          '4XX': { description: 'Client error' },
          '201': {
            description: 'Created',
            headers: { Location: { schema: { type: 'string' } } },
            content: {
              'application/json': {
                schema: { $ref: '#/components/schemas/pet' },
                examples: { rex: { value: { id: 'p-1', name: 'Rex' } } },
              },
              'application/xml': { schema: { $ref: '#/components/schemas/pet' } },
            },
          },
          '409': { $ref: '#/components/responses/conflict' },
        },
      },
    },
    '/pets/{id}': { delete: { operationId: 'deletePet', responses: { '204': { description: 'Deleted' } } } },
  },
  components: {
    requestBodies: {
      newPet: {
        required: true,
        description: 'The pet to add.',
        content: {
          'application/json': { schema: { $ref: '#/components/schemas/newPet' }, example: { name: 'Rex' } },
          'application/x-www-form-urlencoded': { schema: { $ref: '#/components/schemas/newPet' } },
        },
      },
    },
    responses: { conflict: { description: 'Already exists' } },
    schemas: {
      newPet: { type: 'object', required: ['name'], properties: { name: { type: 'string' } } },
      pet: { type: 'object', properties: { id: { type: 'string' }, name: { type: 'string' } } },
    },
  },
}

const swaggerApi = {
  swagger: '2.0',
  'x-scalar-original-source-url': '/swagger.json',
  consumes: ['application/json'],
  paths: {
    '/owners': {
      post: {
        operationId: 'createOwner',
        parameters: [{ in: 'body', name: 'owner', required: true, schema: { $ref: '#/definitions/owner' } }],
        responses: { '200': { description: 'OK', schema: { $ref: '#/definitions/owner' }, examples: { 'application/json': { id: 1 } } } },
      },
    },
  },
  definitions: { owner: { type: 'object', properties: { id: { type: 'integer' } } } },
}

const documents = { pets: petsApi, owners: swaggerApi }

const arazzo: Document = {
  arazzo: '1.1.0',
  sourceDescriptions: [
    { name: 'pets', url: '/openapi.json', type: 'openapi' },
    { name: 'owners', url: '/swagger.json', type: 'openapi' },
  ],
  workflows: [
    {
      workflowId: 'adopt',
      steps: [
        {
          stepId: 'create',
          operationId: '$sourceDescriptions.pets.createPet',
          requestBody: { contentType: 'application/x-www-form-urlencoded', payload: 'name=Rex' },
          successCriteria: [{ condition: '$statusCode == 201' }],
          onFailure: [
            { name: 'exists', type: 'end', criteria: [{ condition: '$statusCode == 409' }] },
            { name: 'badRequest', type: 'end', criteria: [{ condition: '400 == $statusCode' }] },
          ],
        },
      ],
    },
  ],
}

const targetOf = (step: Omit<ArazzoStep, 'stepId'>) => resolveStepTarget(arazzo, { stepId: 's', ...step }, documents)

beforeEach(() => {
  vi.stubGlobal('location', { href: 'https://app.test/scalar/' })
})

afterEach(() => {
  vi.unstubAllGlobals()
})

describe('operationRequestBody', () => {
  it('follows the request body reference and lists its media types', () => {
    // Act
    const body = operationRequestBody(targetOf({ operationId: '$sourceDescriptions.pets.createPet' }), documents)

    // Assert
    expect(body).toMatchObject({ required: true, description: 'The pet to add.' })
    expect(body!.contents.map((content) => content.contentType)).toEqual(['application/json', 'application/x-www-form-urlencoded'])
    expect(body!.contents[0]).toMatchObject({ schema: petsApi.components.schemas.newPet, example: { name: 'Rex' } })
  })

  it('reads a Swagger 2.0 body parameter with the document consumes', () => {
    // Act
    const body = operationRequestBody(targetOf({ operationId: '$sourceDescriptions.owners.createOwner' }), documents)

    // Assert
    expect(body).toMatchObject({ required: true, contents: [{ contentType: 'application/json', schema: swaggerApi.definitions.owner }] })
  })

  it('is undefined for an operation without a body, or a target that is not loaded', () => {
    // Act / Assert
    expect(operationRequestBody(targetOf({ operationId: '$sourceDescriptions.pets.deletePet' }), documents)).toBeUndefined()
    expect(operationRequestBody(targetOf({ operationId: 'missing' }), documents)).toBeUndefined()
    expect(operationRequestBody(undefined, documents)).toBeUndefined()
  })
})

describe('operationResponses', () => {
  it('lists responses in status order with their content, examples and headers', () => {
    // Act
    const responses = operationResponses(targetOf({ operationId: '$sourceDescriptions.pets.createPet' }), documents)

    // Assert
    expect(responses.map((response) => response.status)).toEqual(['201', '409', '4XX', 'default'])
    expect(responses[0]).toMatchObject({
      description: 'Created',
      headers: ['Location'],
      contents: [
        { contentType: 'application/json', schema: petsApi.components.schemas.pet, example: { id: 'p-1', name: 'Rex' } },
        { contentType: 'application/xml' },
      ],
    })
    expect(responses[1]).toMatchObject({ description: 'Already exists', contents: [] })
  })

  it('reads a Swagger 2.0 response schema and its examples', () => {
    // Act
    const [ok] = operationResponses(targetOf({ operationId: '$sourceDescriptions.owners.createOwner' }), documents)

    // Assert
    expect(ok).toMatchObject({ status: '200', contents: [{ contentType: 'application/json', schema: swaggerApi.definitions.owner, example: { id: 1 } }] })
  })
})

describe('response handling', () => {
  it('reads status checks written either way round', () => {
    // Act / Assert
    expect(statusCodesIn([{ condition: '$statusCode == 200 || $statusCode === 202' }, { condition: '404 == $statusCode' }])).toEqual([
      '200',
      '202',
      '404',
    ])
    expect(statusCodesIn([{ condition: '$response.body#/status == 200' }])).toEqual([])
  })

  it('marks the responses the criteria expect and the actions that handle them, ranges only when no exact status is declared', () => {
    // Arrange
    const step = arazzo.workflows![0]!.steps![0]!
    const responses = operationResponses(targetOf({ operationId: '$sourceDescriptions.pets.createPet' }), documents)

    // Act
    const handling = responseHandling(responses, step.successCriteria, step.onFailure as never)

    // Assert
    expect(handling['201']).toEqual({ success: true, actions: [] })
    expect(handling['409']).toEqual({ success: false, actions: ['exists'] })
    expect(handling['4XX']).toEqual({ success: false, actions: ['badRequest'] })
    expect(handling.default).toEqual({ success: false, actions: [] })
  })

  it('gives a checked status with no exact or range response to `default`', () => {
    // Arrange
    const responses = [
      { status: '201', description: '', contents: [], headers: [], root: {} },
      { status: 'default', description: '', contents: [], headers: [], root: {} },
    ]
    const actions = [{ name: 'invalid', type: 'end' as const, criteria: [{ condition: '$statusCode == 422' }] }]

    // Act
    const handling = responseHandling(responses, [{ condition: '$statusCode == 201' }], actions)

    // Assert
    expect(handling['201']).toEqual({ success: true, actions: [] })
    expect(handling.default).toEqual({ success: false, actions: ['invalid'] })
  })

  it('gives the same status to its range when one is declared, and to `default` when not', () => {
    // Arrange
    const response = (status: string) => ({ status, description: '', contents: [], headers: [], root: {} })
    const actions = [{ name: 'invalid', type: 'end' as const, criteria: [{ condition: '$statusCode == 422' }] }]

    // Act
    const withRange = responseHandling([response('4XX'), response('default')], undefined, actions)
    const withoutRange = responseHandling([response('5XX'), response('default')], undefined, actions)

    // Assert
    expect(withRange['4XX']!.actions).toEqual(['invalid'])
    expect(withRange.default!.actions).toEqual([])
    expect(withoutRange['5XX']!.actions).toEqual([])
    expect(withoutRange.default!.actions).toEqual(['invalid'])
  })
})

describe('Swagger 2.0 path item parameters', () => {
  const swagger = (operationParameters?: unknown[]) => ({
    swagger: '2.0',
    'x-scalar-original-source-url': '/swagger.json',
    paths: {
      '/orders': {
        parameters: [{ in: 'body', name: 'order', required: true, schema: { type: 'object', title: 'inherited' } }],
        post: { operationId: 'createOrder', ...(operationParameters ? { parameters: operationParameters } : {}), responses: {} },
      },
    },
  })
  const bodyOf = (document: unknown) => {
    const docs = { owners: document }
    const target = resolveStepTarget(arazzo, { stepId: 's', operationId: '$sourceDescriptions.owners.createOrder' }, docs)
    return operationRequestBody(target, docs)
  }

  it('reads a body parameter the operation inherits from its path item', () => {
    // Act
    const body = bodyOf(swagger())

    // Assert
    expect(body).toMatchObject({ required: true, contents: [{ contentType: 'application/json', schema: { title: 'inherited' } }] })
  })

  it('lets the operation override the inherited body parameter by name and location', () => {
    // Act
    const body = bodyOf(swagger([{ in: 'body', name: 'order', required: false, schema: { type: 'object', title: 'own' } }]))

    // Assert
    expect(body).toMatchObject({ required: false, contents: [{ schema: { title: 'own' } }] })
  })
})

describe('the rendered step', () => {
  let wrapper: VueWrapper | undefined

  afterEach(() => {
    wrapper?.unmount()
    wrapper = undefined
  })

  it('shows the media type the step sends, the other types, and the responses with what handles them', async () => {
    // Act
    wrapper = mount(ArazzoDocument, {
      attachTo: globalThis.document.body,
      props: {
        document: arazzo,
        documentName: 'flows',
        documents,
        navigation: traverseArazzoDocument({ documentId: 'flows', document: arazzo as unknown as Record<string, unknown> }),
      },
    })
    await flushPromises()

    // Assert
    const body = wrapper.find('.ba-target-body')
    expect(body.find('.ba-block-title').text()).toContain('application/x-www-form-urlencoded')
    expect(body.text()).toContain('Also accepts application/json')
    expect(body.find('.ba-schema-name').text()).toBe('name')
    const rows = wrapper.findAll('.ba-responses > li')
    expect(rows.map((row) => row.find('.ba-status').text())).toEqual(['201', '409', '4XX', 'default'])
    expect(rows[0]!.text()).toContain('Success criteria')
    expect(rows[0]!.find('.ba-status').classes()).toContain('ba-status-2')
    expect(rows[1]!.text()).toContain('exists')
    expect(rows[1]!.find('details').exists()).toBe(false)
  })

  it("draws the response badges with Scalar's Badge, the handled one in solid green like an OpenAPI x-badge", async () => {
    // Act
    wrapper = mount(ArazzoDocument, {
      attachTo: globalThis.document.body,
      props: {
        document: arazzo,
        documentName: 'flows',
        documents,
        navigation: traverseArazzoDocument({ documentId: 'flows', document: arazzo as unknown as Record<string, unknown> }),
      },
    })
    await flushPromises()

    // Assert
    const badges = wrapper.findAllComponents(Badge).filter((badge) => badge.element.closest('.ba-responses'))
    expect(badges.map((badge) => badge.text())).toEqual(['Success criteria', 'exists', 'badRequest'])
    const [handled, action] = badges as [(typeof badges)[number], (typeof badges)[number]]
    expect(handled.classes()).toEqual(expect.arrayContaining(['ba-badge', 'ba-response-handled', 'badge', 'badge-colored']))
    expect(handled.attributes('style')).toContain('--badge-color: var(--scalar-color-green)')
    expect(action.classes()).toEqual(expect.arrayContaining(['ba-badge', 'badge']))
    expect(action.classes()).not.toContain('badge-colored')
  })
})
