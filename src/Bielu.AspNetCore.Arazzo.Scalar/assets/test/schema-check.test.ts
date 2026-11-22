import { describe, expect, it } from 'vitest'
import { exampleWithCheck } from '../src/helpers/example-input'
import { conformsTo, follow, matchesPattern, mayBacktrack } from '../src/helpers/schema-check'

describe('conformsTo', () => {
  it('follows a pointer through an array to the schema it names', () => {
    // Arrange
    const schema = { type: 'object', properties: { a: { oneOf: [{ type: 'integer' }] }, b: { $ref: '#/properties/a/oneOf/0' } } }

    // Act / Assert
    expect(conformsTo({ a: 1, b: 2 }, schema)).toBe(true)
    expect(conformsTo({ a: 1, b: null }, schema)).toBe(false)
    expect(exampleWithCheck(schema)).toEqual({ value: { a: 1, b: 1 }, needsEdits: false })
  })

  it('does not take an array index that is not one', () => {
    // Arrange: `01` and `x` are not array indexes, so the reference cannot be followed and is not held against the value.
    const schema = { properties: { a: { oneOf: [{ type: 'integer' }] }, b: { $ref: '#/properties/a/oneOf/01' }, c: { $ref: '#/properties/a/oneOf/x' } } }

    // Act / Assert
    expect(conformsTo({ a: 1, b: 'text', c: 'text' }, schema)).toBe(true)
  })

  it('applies the schema referred to together with the keywords next to the reference', () => {
    // Arrange
    const schema = { $defs: { atLeastTen: { type: 'integer', minimum: 10 } }, properties: { n: { $ref: '#/$defs/atLeastTen', minimum: 0 } } }
    const resolved = { properties: { n: { $ref: 'elsewhere.json', '$ref-value': { type: 'integer', minimum: 10 }, minimum: 0 } } }

    // Act / Assert
    expect(conformsTo({ n: 5 }, schema)).toBe(false)
    expect(conformsTo({ n: 10 }, schema)).toBe(true)
    expect(conformsTo({ n: 5 }, resolved)).toBe(false)
    expect(exampleWithCheck(schema)?.needsEdits).toBe(true)
  })

  it('follows a reference to a reference, and ends a cycle', () => {
    // Arrange
    const schema = {
      $defs: { a: { $ref: '#/$defs/b' }, b: { type: 'string', minLength: 3 }, loopA: { $ref: '#/$defs/loopB' }, loopB: { $ref: '#/$defs/loopA' } },
      properties: { name: { $ref: '#/$defs/a' }, loop: { $ref: '#/$defs/loopA' } },
    }

    // Act / Assert
    expect(conformsTo({ name: 'ab' }, schema)).toBe(false)
    expect(conformsTo({ name: 'abc', loop: 'anything' }, schema)).toBe(true)
    expect(follow({ $ref: '#/$defs/a' }, [schema]).schema).toEqual({ type: 'string', minLength: 3 })
    // A cycle of references and nothing else says nothing about the value.
    expect(exampleWithCheck(schema)).toEqual({ value: { name: 'string', loop: null }, needsEdits: false })
  })

  it('compares objects without regard to the order of their properties', () => {
    // Arrange
    const unique = { type: 'array', uniqueItems: true }

    // Act / Assert
    expect(conformsTo([{ a: 1, b: 2 }, { b: 2, a: 1 }], unique)).toBe(false)
    expect(conformsTo([[1, 2], [2, 1]], unique)).toBe(true)
    expect(conformsTo({ b: 2, a: { d: 4, c: 3 } }, { enum: [{ a: { c: 3, d: 4 }, b: 2 }] })).toBe(true)
    expect(conformsTo({ b: 2, a: 1 }, { const: { a: 1, b: 2 } })).toBe(true)
    expect(conformsTo([2, 1], { const: [1, 2] })).toBe(false)
  })

  it('rejects every value for a false schema and allows every value for a true one', () => {
    // Arrange
    const schema = { type: 'object', properties: { disabled: false, open: true }, $defs: { never: false }, additionalProperties: { $ref: '#/$defs/never' } }

    // Act / Assert
    expect(conformsTo({ disabled: null }, schema)).toBe(false)
    expect(conformsTo({ open: 'anything' }, schema)).toBe(true)
    expect(conformsTo({ other: 1 }, schema)).toBe(false)
    expect(conformsTo(1, false)).toBe(false)
    expect(conformsTo(1, true)).toBe(true)
    expect(conformsTo('a', { allOf: [true, false] })).toBe(false)
    expect(exampleWithCheck(schema)).toEqual({ value: { open: null }, needsEdits: false })
  })
})

describe('matchesPattern', () => {
  it('does not run a pattern that may backtrack catastrophically', () => {
    // Arrange
    const value = `${'a'.repeat(100)}c`

    // Act
    const started = performance.now()
    const matched = matchesPattern(value, '(a*)*b')

    // Assert: not held against the value, and returned at once.
    expect(matched).toBe(true)
    expect(performance.now() - started).toBeLessThan(100)
  })

  it('still checks ordinary patterns, and leaves out very long values', () => {
    // Act / Assert
    expect(matchesPattern('123', '^\\d+$')).toBe(true)
    expect(matchesPattern('12a', '^\\d+$')).toBe(false)
    expect(matchesPattern('x'.repeat(1001), '^\\d+$')).toBe(true)
  })

  it.each([
    ['(a*)*b', true],
    ['(a+)+$', true],
    ['^(a|aa)+$', true],
    ['((ab)*c)+', true],
    ['(\\d{1,3}){2,}', true],
    ['^[a-z]+$', false],
    ['^(?:ab)+$', false],
    ['^\\d{3}-\\d{4}$', false],
    ['^[(a*)]+$', false],
    ['\\(a*\\)*', false],
    ['^(a{2})+$', false],
  ])('flags %s as a backtracking risk: %s', (pattern, risky) => {
    // Act / Assert
    expect(mayBacktrack(pattern)).toBe(risky)
  })
})

describe('exampleWithCheck limits', () => {
  it('bounds the whole example, not only each array, and says it needs edits', () => {
    // Arrange: six nested arrays of minItems 50 would ask for 50⁶ values.
    let schema: Record<string, unknown> = { type: 'integer' }
    for (let level = 0; level < 6; level++) {
      schema = { type: 'array', minItems: 50, items: schema }
    }
    const count = (value: unknown): number => (Array.isArray(value) ? value.reduce((sum: number, item) => sum + count(item), 1) : 1)

    // Act
    const example = exampleWithCheck({ type: 'object', properties: { grid: schema } })!

    // Assert
    expect(count(example.value)).toBeLessThanOrEqual(1001)
    expect(example.needsEdits).toBe(true)
  })

  it('keeps small arrays whole', () => {
    // Act / Assert
    expect(exampleWithCheck({ type: 'array', minItems: 3, items: { type: 'array', minItems: 2, items: { type: 'integer' } } })).toEqual({
      value: [
        [1, 1],
        [1, 1],
        [1, 1],
      ],
      needsEdits: false,
    })
  })

  it('caps a made-up string before padding it, and says it needs edits', () => {
    // Act
    const example = exampleWithCheck({ type: 'string', minLength: 1_000_000_000_000 })!

    // Assert
    expect((example.value as string).length).toBe(1000)
    expect(example.needsEdits).toBe(true)
    expect(exampleWithCheck({ type: 'string', minLength: 8 })).toEqual({ value: 'stringxx', needsEdits: false })
  })
})
