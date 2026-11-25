import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'
import { parseJsonPointer, resolveStepTarget } from '../src/helpers/resolve-target'
import type { ArazzoDocument } from '../src/types'

const petsApi = {
  openapi: '3.1.0',
  info: { title: 'Pets' },
  'x-scalar-original-source-url': 'https://app.test/openapi/v1.json',
  paths: {
    '/pets/{id}': {
      get: { operationId: 'getPet', summary: 'Get a pet' },
      put: { $ref: '#/components/pathItems/updatePet' },
    },
  },
  components: { pathItems: { updatePet: { operationId: 'updatePet' } } },
}

const eventsApi = {
  asyncapi: '3.0.0',
  info: { title: 'Events' },
  // Configured relative, as Scalar.AspNetCore does.
  'x-scalar-original-source-url': '/asyncapi/v1.json',
  channels: { petAdopted: { address: 'pets.adopted' } },
  operations: {
    onPetAdopted: { action: 'receive', channel: { $ref: '#/channels/petAdopted' } },
  },
}

const documents = { pets: petsApi, events: eventsApi }

const arazzo: ArazzoDocument = {
  arazzo: '1.0.1',
  sourceDescriptions: [
    { name: 'petsApi', url: '/openapi/v1.json', type: 'openapi' },
    { name: 'eventsApi', url: '/asyncapi/v1.json', type: 'asyncapi' },
  ],
  workflows: [{ workflowId: 'adopt', summary: 'Adopt a pet' }],
}

beforeEach(() => {
  vi.stubGlobal('location', { href: 'https://app.test/scalar/' })
})

afterEach(() => {
  vi.unstubAllGlobals()
})

describe('parseJsonPointer', () => {
  it('unescapes ~1/~0 and percent-encoding', () => {
    // Arrange / Act / Assert
    expect(parseJsonPointer('/paths/~1pets~1%7Bid%7D/get')).toEqual(['paths', '/pets/{id}', 'get'])
    expect(parseJsonPointer('/a~0b')).toEqual(['a~b'])
  })

  it('keeps a segment that is not valid percent-encoding', () => {
    // Arrange / Act / Assert
    expect(parseJsonPointer('/100%')).toEqual(['100%'])
  })
})

describe('resolveStepTarget', () => {
  it('resolves an OpenAPI operationId across source descriptions', () => {
    // Arrange / Act
    const target = resolveStepTarget(arazzo, { stepId: 's', operationId: 'getPet' }, documents)

    // Assert
    expect(target).toMatchObject({
      kind: 'operation',
      identifier: 'getPet',
      documentName: 'pets',
      method: 'get',
      path: '/pets/{id}',
      operation: { summary: 'Get a pet' },
    })
  })

  it('follows a $ref to find an operation', () => {
    // Arrange / Act
    const target = resolveStepTarget(arazzo, { stepId: 's', operationId: 'updatePet' }, documents)

    // Assert
    expect(target).toMatchObject({ kind: 'operation', method: 'put', path: '/pets/{id}' })
  })

  it('prefers the $ref-value Scalar resolved', () => {
    // Arrange
    const resolved = {
      pets: {
        ...petsApi,
        paths: { '/x': { get: { $ref: '#/nowhere', '$ref-value': { operationId: 'fromStore' } } } },
      },
    }

    // Act
    const target = resolveStepTarget(arazzo, { stepId: 's', operationId: 'fromStore' }, resolved)

    // Assert
    expect(target).toMatchObject({ kind: 'operation', path: '/x', method: 'get' })
  })

  it('does not take an inherited property for an AsyncAPI operation', () => {
    // Arrange
    const withConstructor = {
      ...documents,
      pets: { ...petsApi, paths: { '/build': { post: { operationId: 'constructor' } } } },
    }
    const eventsFirst: ArazzoDocument = { ...arazzo, sourceDescriptions: [...arazzo.sourceDescriptions!].reverse() }

    // Act
    const target = resolveStepTarget(eventsFirst, { stepId: 's', operationId: 'constructor' }, withConstructor)

    // Assert
    expect(target).toMatchObject({ kind: 'operation', documentName: 'pets', path: '/build', method: 'post' })
  })

  it('resolves an operationPath through a referenced Path Item', () => {
    // Arrange
    const referenced = {
      pets: {
        ...petsApi,
        paths: { '/pets': { $ref: '#/components/pathItems/pets' } },
        components: { pathItems: { pets: { get: { operationId: 'listPets' } } } },
      },
    }
    const operationPath = '{$sourceDescriptions.petsApi.url}#/paths/~1pets/get'

    // Act
    const target = resolveStepTarget(arazzo, { stepId: 's', operationPath }, referenced)

    // Assert
    expect(target).toMatchObject({ kind: 'operation', path: '/pets', method: 'get', operation: { operationId: 'listPets' } })
  })

  it('stops following a cyclic reference', () => {
    // Arrange
    const cyclic = {
      pets: { ...petsApi, paths: { '/a': { $ref: '#/paths/~1b' }, '/b': { $ref: '#/paths/~1a' } } },
    }
    const operationPath = '{$sourceDescriptions.petsApi.url}#/paths/~1a/get'

    // Act
    const target = resolveStepTarget(arazzo, { stepId: 's', operationPath }, cyclic)

    // Assert
    expect(target).toMatchObject({ kind: 'operation', identifier: operationPath, operation: undefined })
  })

  it('narrows a qualified operationId to its source', () => {
    // Arrange / Act
    const wrongSource = resolveStepTarget(arazzo, { stepId: 's', operationId: '$sourceDescriptions.eventsApi.getPet' }, documents)
    const rightSource = resolveStepTarget(arazzo, { stepId: 's', operationId: '$sourceDescriptions.petsApi.getPet' }, documents)

    // Assert
    expect(wrongSource).toMatchObject({ kind: 'operation', documentName: undefined })
    expect(rightSource).toMatchObject({ kind: 'operation', documentName: 'pets', path: '/pets/{id}' })
  })

  it('resolves an AsyncAPI operation id and its channel', () => {
    // Arrange / Act
    const target = resolveStepTarget(arazzo, { stepId: 's', operationId: 'onPetAdopted' }, documents)

    // Assert
    expect(target).toMatchObject({
      kind: 'channel',
      documentName: 'events',
      channelName: 'petAdopted',
      channel: { address: 'pets.adopted' },
      operation: { action: 'receive' },
    })
  })

  it('resolves an operationPath', () => {
    // Arrange
    const operationPath = '{$sourceDescriptions.petsApi.url}#/paths/~1pets~1{id}/get'

    // Act
    const target = resolveStepTarget(arazzo, { stepId: 's', operationPath }, documents)

    // Assert
    expect(target).toMatchObject({ kind: 'operation', identifier: operationPath, method: 'get', path: '/pets/{id}' })
  })

  it('resolves a channelPath', () => {
    // Arrange
    const channelPath = '{$sourceDescriptions.eventsApi.url}#/channels/petAdopted'

    // Act
    const target = resolveStepTarget(arazzo, { stepId: 's', channelPath }, documents)

    // Assert
    expect(target).toMatchObject({ kind: 'channel', channelName: 'petAdopted', channel: { address: 'pets.adopted' } })
  })

  it('resolves an operationPath into AsyncAPI operations', () => {
    // Arrange / Act
    const target = resolveStepTarget(
      arazzo,
      { stepId: 's', operationPath: '{$sourceDescriptions.eventsApi.url}#/operations/onPetAdopted' },
      documents,
    )

    // Assert
    expect(target).toMatchObject({ kind: 'channel', channelName: 'petAdopted' })
  })

  it('resolves a workflowId in the same document', () => {
    // Arrange / Act
    const target = resolveStepTarget(arazzo, { stepId: 's', workflowId: 'adopt' }, documents)

    // Assert
    expect(target).toMatchObject({ kind: 'workflow', identifier: 'adopt', workflow: { summary: 'Adopt a pet' } })
  })

  it.each([
    [{ stepId: 's', operationId: 'unknown' }, 'operation'],
    [{ stepId: 's', operationPath: '{$sourceDescriptions.missing.url}#/paths/~1x/get' }, 'operation'],
    [{ stepId: 's', operationPath: 'not-an-expression' }, 'operation'],
    [{ stepId: 's', channelPath: '{$sourceDescriptions.eventsApi.url}#/channels/missing' }, 'channel'],
  ])('keeps the identifier of a target it cannot resolve: %j', (step, kind) => {
    // Arrange / Act
    const target = resolveStepTarget(arazzo, step, documents)

    // Assert
    expect(target).toMatchObject({ kind, documentName: undefined })
  })

  it('does not resolve when the source document is not loaded', () => {
    // Arrange / Act
    const target = resolveStepTarget(arazzo, { stepId: 's', operationId: 'getPet' }, {})

    // Assert
    expect(target).toMatchObject({ kind: 'operation', identifier: 'getPet', documentName: undefined })
  })

  it('resolves a source loaded from another URL by the document\'s $self', () => {
    // Arrange: Scalar loaded the document through service discovery, while the source names its public URL
    const authApi = {
      openapi: '3.2.0',
      $self: 'https://login.test/openapi/v1.json',
      'x-scalar-original-source-url': 'https://authservice/openapi/v1.json',
      paths: { '/connect/token': { post: { operationId: 'token' } } },
    }
    const withAuth: ArazzoDocument = {
      ...arazzo,
      sourceDescriptions: [{ name: 'auth', url: 'https://login.test/openapi/v1.json', type: 'openapi' }],
    }

    // Act
    const target = resolveStepTarget(withAuth, { stepId: 's', operationId: 'token' }, { auth: authApi })

    // Assert
    expect(target).toMatchObject({ kind: 'operation', identifier: 'token', documentName: 'auth', sourceName: 'auth' })
  })

  it('resolves a relative $self against where the document was loaded from', () => {
    // Arrange
    const authApi = {
      openapi: '3.2.0',
      $self: '/openapi/v1.json',
      'x-scalar-original-source-url': 'https://login.test/docs/openapi.json',
      paths: { '/connect/token': { post: { operationId: 'token' } } },
    }
    const withAuth: ArazzoDocument = {
      ...arazzo,
      sourceDescriptions: [{ name: 'auth', url: 'https://login.test/openapi/v1.json', type: 'openapi' }],
    }

    // Act
    const target = resolveStepTarget(withAuth, { stepId: 's', operationId: 'token' }, { auth: authApi })

    // Assert
    expect(target).toMatchObject({ documentName: 'auth' })
  })

  it('returns undefined for a step without a target', () => {
    // Arrange / Act / Assert
    expect(resolveStepTarget(arazzo, { stepId: 's' }, documents)).toBeUndefined()
  })
})
