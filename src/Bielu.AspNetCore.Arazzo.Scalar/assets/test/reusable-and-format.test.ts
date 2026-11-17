import { describe, expect, it } from 'vitest'
import { formatValue, schemaPropertyRows } from '../src/helpers/format'
import { resolveReusable, resolveReusables } from '../src/helpers/reusable'
import type { ArazzoDocument, ArazzoParameter } from '../src/types'

const document: ArazzoDocument = {
  arazzo: '1.0.1',
  workflows: [],
  components: {
    parameters: {
      page: { name: 'page', in: 'query', value: 1 },
    },
  },
}

describe('resolveReusable', () => {
  it('returns an inline entry as is', () => {
    // Arrange
    const parameter: ArazzoParameter = { name: 'id', in: 'path', value: '$inputs.id' }

    // Act / Assert
    expect(resolveReusable(document, parameter)).toBe(parameter)
  })

  it('resolves a component reference', () => {
    // Arrange / Act
    const parameter = resolveReusable<ArazzoParameter>(document, { reference: '$components.parameters.page' })

    // Assert
    expect(parameter).toEqual({ name: 'page', in: 'query', value: 1 })
  })

  it('lets the reference override the component value', () => {
    // Arrange / Act
    const parameter = resolveReusable<ArazzoParameter>(document, { reference: '$components.parameters.page', value: 2 })

    // Assert
    expect(parameter).toEqual({ name: 'page', in: 'query', value: 2 })
  })

  it.each(['$components.parameters.missing', '$components.nothing.page', 'page'])(
    'returns undefined for a dangling reference: %s',
    (reference) => {
      // Arrange / Act / Assert
      expect(resolveReusable(document, { reference })).toBeUndefined()
    },
  )
})

describe('resolveReusables', () => {
  it('resolves references and drops the dangling ones', () => {
    // Arrange / Act
    const parameters = resolveReusables<ArazzoParameter>(document, [
      { name: 'id', value: 1 },
      { reference: '$components.parameters.page' },
      { reference: '$components.parameters.missing' },
    ])

    // Assert
    expect(parameters.map((parameter) => parameter.name)).toEqual(['id', 'page'])
  })

  it('treats a missing list as empty', () => {
    // Arrange / Act / Assert
    expect(resolveReusables(document, undefined)).toEqual([])
  })
})

describe('formatValue', () => {
  it.each([
    ['$response.body#/id', '$response.body#/id'],
    [42, '42'],
    [undefined, ''],
    [{ a: 1 }, '{\n  "a": 1\n}'],
  ])('formats %j', (value, expected) => {
    // Arrange / Act / Assert
    expect(formatValue(value)).toBe(expected)
  })
})

describe('schemaPropertyRows', () => {
  it('flattens the top-level properties of an object schema', () => {
    // Arrange
    const schema = {
      type: 'object',
      required: ['petId'],
      properties: {
        petId: { type: 'string', description: 'The pet' },
        tags: { type: ['array', 'null'] },
        owner: { $ref: '#/components/inputs/owner' },
        extra: {},
      },
    }

    // Act
    const rows = schemaPropertyRows(schema)

    // Assert
    expect(rows).toEqual([
      { name: 'petId', type: 'string', required: true, description: 'The pet' },
      { name: 'tags', type: 'array | null', required: false, description: '' },
      { name: 'owner', type: 'object', required: false, description: '' },
      { name: 'extra', type: 'any', required: false, description: '' },
    ])
  })

  it.each([undefined, 'string', { type: 'string' }])('returns undefined for a schema without properties: %j', (schema) => {
    // Arrange / Act / Assert
    expect(schemaPropertyRows(schema)).toBeUndefined()
  })
})
