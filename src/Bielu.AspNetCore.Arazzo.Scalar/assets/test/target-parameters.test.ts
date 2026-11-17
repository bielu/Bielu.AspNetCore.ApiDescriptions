import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'
import { resolveStepTarget, targetDescription, targetParameters } from '../src/helpers/resolve-target'
import type { ArazzoDocument } from '../src/types'

describe('targetParameters', () => {
  const api = {
    openapi: '3.1.0',
    'x-scalar-original-source-url': 'https://app.test/openapi.json',
    paths: {
      '/orders/{id}': {
        parameters: [{ $ref: '#/components/parameters/id' }, { name: 'trace', in: 'header' }],
        get: {
          operationId: 'getOrder',
          summary: 'Get an order',
          description: 'Returns the order.',
          parameters: [{ name: 'trace', in: 'header', required: true, description: 'Trace id' }],
        },
      },
    },
    components: { parameters: { id: { name: 'id', in: 'path', schema: { type: 'string', format: 'uuid' } } } },
  }
  const events = {
    asyncapi: '3.0.0',
    'x-scalar-original-source-url': 'https://app.test/asyncapi.json',
    channels: { order: { address: 'orders.{id}', description: 'Order events', parameters: { id: { description: 'Order id' } } } },
    operations: { onOrder: { action: 'receive', channel: { $ref: '#/channels/order' } } },
  }
  const arazzo: ArazzoDocument = {
    arazzo: '1.0.1',
    sourceDescriptions: [
      { name: 'api', url: 'https://app.test/openapi.json' },
      { name: 'events', url: 'https://app.test/asyncapi.json', type: 'asyncapi' },
    ],
  }
  const documents = { api, events }

  beforeEach(() => {
    vi.stubGlobal('location', { href: 'https://app.test/scalar/' })
  })

  afterEach(() => {
    vi.unstubAllGlobals()
  })

  it('merges path item and operation parameters, the operation winning', () => {
    // Arrange
    const target = resolveStepTarget(arazzo, { stepId: 's', operationId: 'getOrder' }, documents)

    // Act
    const parameters = targetParameters(target, documents)

    // Assert
    expect(parameters).toEqual([
      { name: 'id', location: 'path', required: true, type: 'string (uuid)', description: '' },
      { name: 'trace', location: 'header', required: true, type: 'string', description: 'Trace id' },
    ])
    expect(targetDescription(target)).toBe('Returns the order.')
  })

  it('lists a channel target’s address parameters', () => {
    // Arrange
    const target = resolveStepTarget(arazzo, { stepId: 's', operationId: 'onOrder' }, documents)

    // Act
    const parameters = targetParameters(target, documents)

    // Assert
    expect(parameters).toEqual([{ name: 'id', location: 'channel', required: true, type: 'string', description: 'Order id' }])
    expect(targetDescription(target)).toBe('Order events')
  })

  it('returns nothing for an unresolved or workflow target', () => {
    // Arrange
    const unresolved = resolveStepTarget(arazzo, { stepId: 's', operationId: 'nope' }, documents)

    // Act / Assert
    expect(targetParameters(unresolved, documents)).toEqual([])
    expect(targetParameters({ kind: 'workflow', identifier: 'wf', workflow: undefined }, documents)).toEqual([])
    expect(targetDescription(undefined)).toBe('')
  })
})
