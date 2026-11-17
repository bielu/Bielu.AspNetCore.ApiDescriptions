import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'
import { schemaPropertyRows } from '../src/helpers/format'
import { resolveStepTarget, targetMessages } from '../src/helpers/resolve-target'
import type { ArazzoDocument, ArazzoStep } from '../src/types'

const eventsApi = {
  asyncapi: '3.0.0',
  defaultContentType: 'application/json',
  'x-scalar-original-source-url': '/asyncapi/v1.json',
  channels: {
    orderPlaced: {
      address: 'orders.placed',
      messages: {
        orderPlaced: { $ref: '#/components/messages/orderPlaced' },
        orderAmended: { $ref: '#/components/messages/orderAmended' },
      },
    },
    audit: {
      address: 'audit',
      messages: { auditRecord: { $ref: '#/components/messages/auditRecord' } },
    },
  },
  operations: {
    onOrderPlaced: {
      action: 'send',
      channel: { $ref: '#/channels/orderPlaced' },
      messages: [{ $ref: '#/channels/orderPlaced/messages/orderPlaced' }],
    },
  },
  components: {
    messages: {
      orderPlaced: {
        title: 'Order placed',
        summary: 'An order was accepted.',
        payload: { $ref: '#/components/schemas/order' },
        examples: [{ name: 'small', payload: { id: 'o-1', lines: [{ sku: 'A', quantity: 1 }] } }],
      },
      orderAmended: { contentType: 'application/cloudevents+json', payload: { type: 'object', properties: { id: { type: 'string' } } } },
      auditRecord: {
        payload: { schemaFormat: 'application/vnd.apache.avro;version=1.9.0', schema: { type: 'record', name: 'Audit', fields: [] } },
      },
    },
    schemas: {
      order: {
        type: 'object',
        required: ['id'],
        properties: {
          id: { type: 'string', format: 'uuid' },
          lines: { type: 'array', items: { $ref: '#/components/schemas/line' } },
        },
      },
      line: { type: 'object', properties: { sku: { type: 'string' }, quantity: { type: 'integer', minimum: 1 } } },
    },
  },
}

const documents = { events: eventsApi }

const arazzo: ArazzoDocument = {
  arazzo: '1.1.0',
  sourceDescriptions: [{ name: 'events', url: '/asyncapi/v1.json', type: 'asyncapi' }],
  workflows: [],
}

const messagesOf = (step: Omit<ArazzoStep, 'stepId'>, loaded: Record<string, unknown> = documents) =>
  targetMessages(resolveStepTarget(arazzo, { stepId: 's', ...step }, loaded), loaded)

beforeEach(() => {
  vi.stubGlobal('location', { href: 'https://app.test/scalar/' })
})

afterEach(() => {
  vi.unstubAllGlobals()
})

describe('targetMessages', () => {
  it('lists only the messages an operation target declares, with its payload and first example', () => {
    // Arrange / Act
    const messages = messagesOf({ operationId: 'onOrderPlaced' })

    // Assert
    expect(messages).toHaveLength(1)
    expect(messages[0]).toMatchObject({
      name: 'orderPlaced',
      title: 'Order placed',
      summary: 'An order was accepted.',
      contentType: 'application/json',
      payload: eventsApi.components.schemas.order,
      schemaFormat: undefined,
      example: { id: 'o-1', lines: [{ sku: 'A', quantity: 1 }] },
    })
  })

  it("lists every message of a channel target, with each message's own content type", () => {
    // Arrange / Act
    const messages = messagesOf({ channelPath: '{$sourceDescriptions.events.url}#/channels/orderPlaced', action: 'receive' })

    // Assert
    expect(messages.map((message) => [message.name, message.contentType])).toEqual([
      ['orderPlaced', 'application/json'],
      ['orderAmended', 'application/cloudevents+json'],
    ])
  })

  it('unwraps a Multi Format Schema Object and keeps a non-JSON schema format', () => {
    // Arrange / Act
    const [message] = messagesOf({ channelPath: '{$sourceDescriptions.events.url}#/channels/audit' })

    // Assert
    expect(message!.schemaFormat).toBe('application/vnd.apache.avro;version=1.9.0')
    expect(message!.payload).toEqual({ type: 'record', name: 'Audit', fields: [] })
  })

  it('is empty for OpenAPI, workflow and unresolved targets', () => {
    // Arrange / Act / Assert
    expect(messagesOf({ workflowId: 'other' })).toEqual([])
    expect(messagesOf({ operationId: 'onOrderPlaced' }, {})).toEqual([])
    expect(targetMessages(undefined, documents)).toEqual([])
  })
})

describe('schemaPropertyRows with a document root', () => {
  it('follows $refs into the document the schema comes from, including the schema itself being one', () => {
    // Arrange / Act
    const rows = schemaPropertyRows({ $ref: '#/components/schemas/order' }, eventsApi)!

    // Assert
    expect(rows.map((row) => [row.name, row.type, row.required])).toEqual([
      ['id', 'string', true],
      ['lines', 'array<object>', false],
    ])
    expect(rows[1]!.children!.map((row) => row.name)).toEqual(['sku', 'quantity'])
  })

  it('reads the type of an enum or const that states none from its values', () => {
    // Arrange
    const schema = {
      type: 'object',
      properties: {
        status: { enum: ['Succeeded', 'Declined'] },
        priority: { enum: [1, 2, null] },
        kind: { const: 'order' },
        anything: {},
      },
    }

    // Act
    const rows = schemaPropertyRows(schema)!

    // Assert
    expect(rows.map((row) => [row.name, row.type])).toEqual([
      ['status', 'string'],
      ['priority', 'number | null'],
      ['kind', 'string'],
      ['anything', 'any'],
    ])
  })

  it("uses the target Scalar's workspace store already resolved", () => {
    // Arrange
    const schema = { $ref: '#/elsewhere', '$ref-value': { type: 'object', properties: { a: { type: 'string' } } } }

    // Act / Assert
    expect(schemaPropertyRows(schema, {})!.map((row) => row.name)).toEqual(['a'])
  })
})
