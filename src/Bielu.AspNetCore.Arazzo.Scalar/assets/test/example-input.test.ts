// @vitest-environment happy-dom
import { flushPromises, mount, type VueWrapper } from '@vue/test-utils'
import { afterEach, describe, expect, it, vi } from 'vitest'
import ArazzoWorkflow from '../src/components/ArazzoWorkflow.vue'
import { exampleFromSchema, exampleWithCheck } from '../src/helpers/example-input'
import { toWorkflowViews, traverseArazzoDocument } from '../src/helpers/navigation'
import type { ArazzoDocument } from '../src/types'

describe('exampleFromSchema', () => {
  it('makes a value of each type, with samples for known string formats', () => {
    // Arrange
    const schema = {
      type: 'object',
      properties: {
        sku: { type: 'string' },
        id: { type: 'string', format: 'uuid' },
        at: { type: 'string', format: 'date-time' },
        code: { type: 'string', minLength: 8 },
        quantity: { type: 'integer', minimum: 1 },
        price: { type: 'number', exclusiveMinimum: 0 },
        gift: { type: 'boolean' },
        note: { type: ['string', 'null'] },
        count: { type: ['string', 'integer'], pattern: '^-?(?:0|[1-9]\\d*)$' },
        empty: { type: ['null'] },
        nothing: { type: 'null' },
        tags: { type: 'array', items: { type: 'string' } },
        pair: { type: 'array', items: { type: 'integer' }, minItems: 2 },
        unknown: {},
      },
    }

    // Act / Assert
    expect(exampleFromSchema(schema)).toEqual({
      sku: 'string',
      id: '3fa85f64-5717-4562-b3fc-2c963f66afa6',
      at: '2026-01-01T12:00:00Z',
      code: 'stringxx',
      quantity: 1,
      price: 1,
      gift: true,
      note: 'string',
      count: 1,
      empty: null,
      nothing: null,
      tags: ['string'],
      pair: [1, 1],
      unknown: null,
    })
  })

  it('prefers what the schema says: example, examples, const, default, enum', () => {
    // Act / Assert
    expect(exampleFromSchema({ type: 'string', example: 'SKU-1', default: 'x' })).toBe('SKU-1')
    expect(exampleFromSchema({ type: 'string', examples: ['first', 'second'] })).toBe('first')
    expect(exampleFromSchema({ const: 'fixed' })).toBe('fixed')
    expect(exampleFromSchema({ type: 'integer', default: 5 })).toBe(5)
    expect(exampleFromSchema({ enum: ['express', 'standard'] })).toBe('express')
  })

  it('follows references in the schema and then in the document, and stops on recursion', () => {
    // Arrange
    const document = { components: { inputs: { address: { type: 'object', properties: { city: { type: 'string', example: 'Oslo' } } } } } }
    const schema = {
      type: 'object',
      properties: {
        shipTo: { $ref: '#/components/inputs/address' },
        line: { $ref: '#/$defs/line' },
      },
      $defs: { line: { type: 'object', properties: { sku: { type: 'string' }, parent: { $ref: '#/$defs/line' } } } },
    }

    // Act / Assert
    expect(exampleFromSchema(schema, document)).toEqual({ shipTo: { city: 'Oslo' }, line: { sku: 'string', parent: {} } })
  })

  it('takes the first alternative of oneOf/anyOf and merges allOf', () => {
    // Act / Assert
    expect(exampleFromSchema({ oneOf: [{ type: 'integer' }, { type: 'string' }] })).toBe(1)
    expect(
      exampleFromSchema({
        allOf: [{ type: 'object', properties: { a: { type: 'string' } } }, { type: 'object', properties: { b: { type: 'boolean' } } }],
        properties: { c: { const: 3 } },
      }),
    ).toEqual({ a: 'string', b: true, c: 3 })
  })

  it('is undefined without a schema', () => {
    // Act / Assert
    expect(exampleFromSchema(undefined)).toBeUndefined()
    expect(exampleWithCheck(undefined)).toBeUndefined()
    expect(exampleFromSchema('nope')).toBeUndefined()
  })

  it('keeps strings within their length bounds', () => {
    // Arrange
    const schema = {
      type: 'object',
      required: ['code'],
      properties: { code: { type: 'string', maxLength: 2 }, ref: { type: 'string', minLength: 3, maxLength: 4 }, long: { type: 'string', minLength: 10 } },
    }

    // Act
    const example = exampleWithCheck(schema)

    // Assert
    expect(example).toEqual({ value: { code: 'st', ref: 'stri', long: 'stringxxxx' }, needsEdits: false })
  })

  it('says the example needs edits when a value cannot satisfy its schema', () => {
    // Act / Assert
    expect(exampleWithCheck({ type: 'string', format: 'uuid', maxLength: 8 })).toEqual({ value: '3fa85f64-5717-4562-b3fc-2c963f66afa6', needsEdits: true })
    expect(exampleWithCheck({ type: 'string', pattern: '^[A-Z]{3}$' })!.needsEdits).toBe(true)
    expect(exampleWithCheck({ type: 'string', minLength: 5, maxLength: 2 })!.needsEdits).toBe(true)
    expect(exampleWithCheck({ type: 'integer', minimum: 5, maximum: 3 })!.needsEdits).toBe(true)
    expect(exampleWithCheck({ type: 'integer', exclusiveMaximum: 1 })!.needsEdits).toBe(true)
    expect(exampleWithCheck({ type: 'string', pattern: '^str' })!.needsEdits).toBe(false)
    expect(exampleWithCheck({ type: 'string', pattern: '(?<' })!.needsEdits).toBe(false)
  })

  it('makes arrays as long as minItems asks, within maxItems', () => {
    // Act / Assert
    expect(exampleWithCheck({ type: 'array', minItems: 4, items: { type: 'integer' } })).toEqual({ value: [1, 1, 1, 1], needsEdits: false })
    expect(exampleWithCheck({ type: 'array', maxItems: 0, items: { type: 'integer' } })).toEqual({ value: [], needsEdits: false })
    expect(exampleWithCheck({ type: 'array', minItems: 3, maxItems: 2, items: { type: 'integer' } })).toEqual({ value: [1, 1], needsEdits: true })
    // Two equal items cannot be unique.
    expect(exampleWithCheck({ type: 'array', minItems: 2, uniqueItems: true, items: { type: 'integer' } })!.needsEdits).toBe(true)
  })

  it('checks the example against the schema, authored or made up', () => {
    // Arrange
    const required = { type: 'object', required: ['sku'], properties: { sku: { type: 'string' } } }

    // Act / Assert
    expect(exampleWithCheck({ ...required, example: { sku: 'SKU-1' } })).toEqual({ value: { sku: 'SKU-1' }, needsEdits: false })
    expect(exampleWithCheck({ ...required, example: { quantity: 2 } })!.needsEdits).toBe(true)
    expect(exampleWithCheck({ type: 'integer', multipleOf: 5, minimum: 3 })).toEqual({ value: 5, needsEdits: false })
    expect(exampleWithCheck({ type: 'integer', multipleOf: 5, minimum: 3, maximum: 4 })!.needsEdits).toBe(true)
    expect(exampleWithCheck({ type: 'object', properties: { tier: { $ref: '#/$defs/tier' } }, $defs: { tier: { enum: ['gold'] } } })).toEqual({
      value: { tier: 'gold' },
      needsEdits: false,
    })
  })
})

describe('the workflow inputs', () => {
  let wrapper: VueWrapper | undefined

  afterEach(() => {
    wrapper?.unmount()
    wrapper = undefined
    vi.unstubAllGlobals()
  })

  it('offer an example input to copy, made from the inputs component the workflow refers to', async () => {
    // Arrange
    const writeText = vi.fn().mockResolvedValue(undefined)
    vi.stubGlobal('navigator', { clipboard: { writeText } })
    const document: ArazzoDocument = {
      arazzo: '1.1.0',
      components: { inputs: { order: { type: 'object', properties: { sku: { type: 'string', example: 'SKU-1' }, quantity: { type: 'integer', minimum: 1 } } } } },
      workflows: [{ workflowId: 'place', inputs: { $ref: '#/components/inputs/order' }, steps: [{ stepId: 'a' }] }],
    }
    const [view] = toWorkflowViews(document, traverseArazzoDocument({ documentId: 'doc', document: document as unknown as Record<string, unknown> }))
    wrapper = mount(ArazzoWorkflow, { props: { view: view!, document, documents: {} } })

    // Act
    const example = wrapper.find('.ba-example-input')
    await example.find('button').trigger('click')
    await flushPromises()

    // Assert
    const expected = JSON.stringify({ sku: 'SKU-1', quantity: 1 }, null, 2)
    expect(example.find('summary').text()).toBe('Example input')
    expect(example.find('pre').text()).toBe(expected)
    expect(writeText).toHaveBeenCalledWith(expected)
    expect(example.text()).toContain('Copied')
    expect(example.find('.ba-example-warning').exists()).toBe(false)
  })

  it('say when the example needs edits before use', () => {
    // Arrange
    const document: ArazzoDocument = {
      arazzo: '1.1.0',
      workflows: [{ workflowId: 'place', inputs: { type: 'object', properties: { code: { type: 'string', pattern: '^[A-Z]{2}$' } } }, steps: [{ stepId: 'a' }] }],
    }
    const [view] = toWorkflowViews(document, traverseArazzoDocument({ documentId: 'doc', document: document as unknown as Record<string, unknown> }))

    // Act
    wrapper = mount(ArazzoWorkflow, { props: { view: view!, document, documents: {} } })

    // Assert
    expect(wrapper.find('.ba-example-input .ba-example-warning').text()).toContain('edit them before use')
  })

  it('offer no example for a workflow without inputs', () => {
    // Arrange
    const document: ArazzoDocument = { arazzo: '1.1.0', workflows: [{ workflowId: 'plain', steps: [{ stepId: 'a' }] }] }
    const [view] = toWorkflowViews(document, traverseArazzoDocument({ documentId: 'doc', document: document as unknown as Record<string, unknown> }))

    // Act
    wrapper = mount(ArazzoWorkflow, { props: { view: view!, document, documents: {} } })

    // Assert
    expect(wrapper.find('.ba-example-input').exists()).toBe(false)
  })
})
