import { describe, expect, it } from 'vitest'
import { extensionsOf, resolveLocalRef } from '../src/helpers/extensions'
import { componentsEntryId, hasComponents, toWorkflowViews, traverseArazzoDocument } from '../src/helpers/navigation'
import type { ArazzoDocument } from '../src/types'

const document: ArazzoDocument = {
  arazzo: '1.1.0',
  workflows: [{ workflowId: 'order', inputs: { $ref: '#/components/inputs/orderInput' }, steps: [{ stepId: 'create' }] }],
  components: {
    inputs: { orderInput: { type: 'object', properties: { sku: { type: 'string' } } } },
    'x-generated': false,
  },
}

describe('extensionsOf', () => {
  it('lists the x- fields in document order and leaves the rest out', () => {
    // Arrange
    const value = { name: 'a', 'x-owner': 'checkout', type: 'end', 'x-cost': { requests: 1 } }

    // Act / Assert
    expect(extensionsOf(value)).toEqual([
      ['x-owner', 'checkout'],
      ['x-cost', { requests: 1 }],
    ])
  })

  it("leaves out Scalar's own x-scalar- bookkeeping", () => {
    // Arrange / Act / Assert
    expect(extensionsOf({ 'x-scalar-original-source-url': '/a.json', 'x-team': 'b' })).toEqual([['x-team', 'b']])
  })

  it('returns nothing for values that are not objects', () => {
    // Arrange / Act / Assert
    expect(extensionsOf(undefined)).toEqual([])
    expect(extensionsOf('x-a')).toEqual([])
    expect(extensionsOf([{ 'x-a': 1 }])).toEqual([])
  })
})

describe('resolveLocalRef', () => {
  it('follows a local $ref through the document', () => {
    // Arrange / Act
    const inputs = resolveLocalRef(document, document.workflows![0]!.inputs)

    // Assert
    expect(inputs).toBe(document.components!.inputs!.orderInput)
  })

  it("prefers the target Scalar's workspace store already resolved", () => {
    // Arrange
    const resolved = { type: 'object' }

    // Act / Assert
    expect(resolveLocalRef(document, { $ref: '#/nowhere', '$ref-value': resolved })).toBe(resolved)
  })

  it('unescapes pointer segments', () => {
    // Arrange
    const root = { 'a/b': { 'c~d': 42 } }

    // Act / Assert
    expect(resolveLocalRef(root, { $ref: '#/a~1b/c~0d' })).toBe(42)
  })

  it('returns the value unchanged when it is no $ref, an external one, or a dangling one', () => {
    // Arrange
    const external = { $ref: 'other.json#/x' }
    const dangling = { $ref: '#/components/inputs/missing' }
    const plain = { type: 'string' }

    // Act / Assert
    expect(resolveLocalRef(document, plain)).toBe(plain)
    expect(resolveLocalRef(document, external)).toBe(external)
    expect(resolveLocalRef(document, dangling)).toBe(dangling)
    expect(resolveLocalRef(document, { $ref: '#/constructor' })).toEqual({ $ref: '#/constructor' })
  })
})

describe('components navigation', () => {
  it('adds a Components entry after the workflows when the document declares any', () => {
    // Arrange / Act
    const entries = traverseArazzoDocument({ documentId: 'doc', document: document as unknown as Record<string, unknown> })

    // Assert
    expect(entries.map((entry) => [entry.id, entry.kind])).toEqual([
      ['doc/workflow/order', 'workflow'],
      ['doc/components', 'components'],
    ])
    expect(componentsEntryId(entries)).toBe('doc/components')
    expect(toWorkflowViews(document, entries).map((view) => view.id)).toEqual(['doc/workflow/order'])
  })

  it('adds no entry for empty components or extensions only', () => {
    // Arrange
    const bare: ArazzoDocument = { arazzo: '1.1.0', workflows: [], components: { inputs: {}, 'x-generated': true } }

    // Act
    const entries = traverseArazzoDocument({ documentId: 'doc', document: bare as unknown as Record<string, unknown> })

    // Assert
    expect(hasComponents(bare)).toBe(false)
    expect(componentsEntryId(entries)).toBeUndefined()
  })
})
