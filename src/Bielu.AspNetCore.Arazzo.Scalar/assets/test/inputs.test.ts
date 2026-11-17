import { describe, expect, it } from 'vitest'
import { schemaPropertyRows } from '../src/helpers/format'

describe('schemaPropertyRows nesting', () => {
  it('nests object and array-of-object properties and lists their details', () => {
    // Arrange
    const schema = {
      type: 'object',
      properties: {
        address: { $ref: '#/$defs/address' },
        lines: { type: 'array', items: { $ref: '#/$defs/line' } },
        status: { type: 'string', enum: ['new', 'paid'], default: 'new' },
      },
      $defs: {
        address: { type: 'object', required: ['city'], properties: { city: { type: 'string', minLength: 1 } } },
        line: { type: 'object', properties: { sku: { type: 'string', format: 'uuid' } } },
      },
    }

    // Act
    const rows = schemaPropertyRows(schema)

    // Assert
    expect(rows).toEqual([
      {
        name: 'address',
        type: 'object',
        required: false,
        description: '',
        children: [{ name: 'city', type: 'string', required: true, description: '', details: ['min length: 1'] }],
      },
      {
        name: 'lines',
        type: 'array<object>',
        required: false,
        description: '',
        children: [{ name: 'sku', type: 'string', required: false, description: '', details: ['uuid'] }],
      },
      { name: 'status', type: 'string', required: false, description: '', details: ['one of: "new", "paid"', 'default: "new"'] },
    ])
  })

  it('stops expanding a recursive reference', () => {
    // Arrange
    const schema = {
      type: 'object',
      properties: { node: { $ref: '#/$defs/node' } },
      $defs: { node: { type: 'object', properties: { next: { $ref: '#/$defs/node' } } } },
    }

    // Act
    const rows = schemaPropertyRows(schema)

    // Assert
    expect(rows![0]!.children![0]).toEqual({ name: 'next', type: 'node', required: false, description: '' })
  })
})
