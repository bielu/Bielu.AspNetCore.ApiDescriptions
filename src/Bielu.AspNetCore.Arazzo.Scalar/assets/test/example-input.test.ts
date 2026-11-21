// @vitest-environment happy-dom
import { flushPromises, mount, type VueWrapper } from '@vue/test-utils'
import { afterEach, describe, expect, it, vi } from 'vitest'
import ArazzoWorkflow from '../src/components/ArazzoWorkflow.vue'
import { exampleFromSchema } from '../src/helpers/example-input'
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
    expect(exampleFromSchema('nope')).toBeUndefined()
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
