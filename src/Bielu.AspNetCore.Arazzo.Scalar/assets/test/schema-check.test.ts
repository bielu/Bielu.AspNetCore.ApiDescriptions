import { describe, expect, it } from 'vitest'
import { exampleWithCheck } from '../src/helpers/example-input'
import { conformsTo, follow, matchesPattern, matchesQuickly, patternShape } from '../src/helpers/schema-check'

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

  it('still checks the keywords next to a reference that closes a cycle', () => {
    // Arrange: `b` refers back to `a`, and adds a minimum of its own.
    const schema = {
      $defs: { a: { $ref: '#/$defs/b' }, b: { $ref: '#/$defs/a', minimum: 10 } },
      properties: { n: { $ref: '#/$defs/a', example: 5 } },
    }

    // Act / Assert
    expect(conformsTo({ n: 5 }, schema)).toBe(false)
    expect(conformsTo({ n: 10 }, schema)).toBe(true)
    expect(exampleWithCheck(schema)).toEqual({ value: { n: 5 }, needsEdits: true })
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

  it('does not run adjacent repeats that overlap on a value long enough to make them slow', () => {
    // Arrange: no repeated group, but eight repeats of the same character backtrack polynomially, n⁹.
    const value = 'a'.repeat(100)

    // Act
    const started = performance.now()
    const matched = matchesPattern(value, '^a*a*a*a*a*a*a*a*b$')

    // Assert
    expect(matched).toBe(true)
    expect(performance.now() - started).toBeLessThan(100)
    expect(matchesQuickly('^a*a*a*a*a*a*a*a*b$', 100)).toBe(false)
    // On a value short enough, the same pattern is still checked.
    expect(matchesPattern('aaa', '^a*a*a*a*a*a*a*a*b$')).toBe(false)
  })

  it('still checks ordinary patterns, and leaves out very long values', () => {
    // Act / Assert
    expect(matchesPattern('123', '^\\d+$')).toBe(true)
    expect(matchesPattern('12a', '^\\d+$')).toBe(false)
    expect(matchesPattern('12.50', '^\\d+(\\.\\d+)?$')).toBe(true)
    expect(matchesPattern('12.', '^\\d+(\\.\\d+)?$')).toBe(false)
    expect(matchesPattern('x'.repeat(1001), '^\\d+$')).toBe(true)
  })

  it('bounds the work of a pattern by its repeats, optional parts and alternatives', () => {
    // Act / Assert: (n + 1)^(repeats + 1) · 2^optionals · choices, within 10⁷.
    expect(matchesQuickly('^\\d+(\\.\\d+)?$', 169)).toBe(true)
    expect(matchesQuickly('^\\d+(\\.\\d+)?$', 170)).toBe(false)
    expect(matchesQuickly('^\\d{3}-\\d{4}$', 1000)).toBe(true)
    expect(matchesQuickly('^(?:red|green|blue)$', 1000)).toBe(true)
  })

  it.each([
    ['(a*)*b', { unbounded: true }],
    ['(a+)+$', { unbounded: true }],
    ['^(a|aa)+$', { unbounded: true }],
    ['((ab)*c)+', { unbounded: true }],
    ['(\\d{1,3}){2,}', { unbounded: true }],
    ['(a+){9}', { unbounded: true }],
    ['^(a?){30}b$', { unbounded: true }],
    [`^(a{${'0'.repeat(40)}1,})+b$`, { unbounded: true }],
    ['^a{1,2', { unbounded: true }],
    ['^a{,3}$', { repeats: 0, unbounded: false }],
    [`^(?<${'n'.repeat(80)}>a)+$`, { repeats: 1, optionals: 0, unbounded: false }],
    ['^((a?)b){2,}$', { unbounded: true }],
    ['^((a|b)c)+$', { unbounded: true }],
    ['^(a)\\1$', { unbounded: true }],
    ['^(?<x>a)\\k<x>$', { unbounded: true }],
    ['^a*a*a*b$', { repeats: 3, optionals: 0, choices: 1, unbounded: false }],
    ['^[a-z]+$', { repeats: 1, unbounded: false }],
    ['^(?:ab)+$', { repeats: 1, optionals: 0, unbounded: false }],
    ['^(?=a)a+?$', { repeats: 1, optionals: 0, unbounded: false }],
    ['^\\d{3}-\\d{4}$', { repeats: 0, unbounded: false }],
    ['^\\d{1,3}$', { repeats: 1, unbounded: false }],
    ['^[(a*)|?]+$', { repeats: 1, optionals: 0, choices: 1, unbounded: false }],
    ['\\(a*\\)*', { repeats: 2, unbounded: false }],
    ['^(a{2})+$', { repeats: 1, unbounded: false }],
    ['^(red|green)-(s|m|l)?$', { repeats: 0, optionals: 1, choices: 6, unbounded: false }],
    ['^(ab){3}(c?)$', { repeats: 0, optionals: 1, unbounded: false }],
  ])('reads the shape of %s', (pattern, shape) => {
    // Act / Assert
    expect(patternShape(pattern)).toMatchObject(shape)
  })

  it('reads a braced quantifier whatever its length, so leading zeros cannot hide a repeat', () => {
    // Arrange: `{0…01,}` is `{1,}`, a repeat inside a repeated group.
    const pattern = `^(a{${'0'.repeat(30)}1,})+b$`

    // Act
    const started = performance.now()
    const matched = matchesPattern('a'.repeat(30), pattern)

    // Assert
    expect(matched).toBe(true)
    expect(performance.now() - started).toBeLessThan(100)
  })

  it('does not run a group repeated a fixed number of times around an optional part', () => {
    // Arrange: each of the 30 copies of `a?` takes `a` or not, 2³⁰ ways, though the pattern has one `?`.
    const value = 'a'.repeat(30)

    // Act
    const started = performance.now()
    const matched = matchesPattern(value, '^(a?){30}b$')

    // Assert
    expect(matched).toBe(true)
    expect(performance.now() - started).toBeLessThan(100)
  })
})

describe('exampleWithCheck limits', () => {
  it('counts an authored example by its size, so arrays cannot repeat a large one into a huge example', () => {
    // Arrange: 100 copies of a million-character example would be about 100 MB of JSON.
    const schema = {
      type: 'object',
      properties: {
        data: { type: 'array', minItems: 10, items: { type: 'array', minItems: 10, items: { type: 'string', example: 'x'.repeat(1_000_000) } } },
      },
    }

    // Act
    const example = exampleWithCheck(schema)!

    // Assert
    expect(JSON.stringify(example.value).length).toBeLessThan(110_000)
    expect(example.needsEdits).toBe(true)
  })

  it('counts the property names it writes, escapes included, against the size of the example', () => {
    // Arrange: 100 copies of an object with a million-character property name would be about 100 MB of JSON.
    const longName = { type: 'array', minItems: 10, items: { type: 'array', minItems: 10, items: { type: 'object', properties: { ['x'.repeat(1_000_000)]: { type: 'integer' } } } } }
    // 40,000 quotes are 80,000 characters once escaped: two copies do not fit, though unescaped they would.
    const quoted = { type: 'array', minItems: 2, items: { type: 'object', properties: { ['"'.repeat(40_000)]: { type: 'integer' } } } }

    // Act
    const long = exampleWithCheck(longName)!
    const escaped = exampleWithCheck(quoted)!

    // Assert
    expect(JSON.stringify(long.value).length).toBeLessThan(110_000)
    expect(long.needsEdits).toBe(true)
    expect(JSON.stringify(escaped.value).length).toBeLessThan(110_000)
    expect(escaped.needsEdits).toBe(true)
    expect(exampleWithCheck({ type: 'object', properties: { name: { type: 'string' } } })).toEqual({ value: { name: 'string' }, needsEdits: false })
  })

  it('keeps authored examples that fit, and fits as many as it can', () => {
    // Arrange: ten 30,000-character examples do not all fit; the ones that do are kept.
    const schema = { type: 'array', minItems: 10, items: { type: 'string', example: 'y'.repeat(30_000) } }

    // Act
    const example = exampleWithCheck(schema)!
    const values = example.value as (string | null)[]

    // Assert
    expect(values.filter((value) => value !== null)).toHaveLength(3)
    expect(values.slice(0, 3).every((value) => value?.length === 30_000)).toBe(true)
    expect(example.needsEdits).toBe(true)
    expect(exampleWithCheck({ type: 'string', example: 'short' })).toEqual({ value: 'short', needsEdits: false })
  })

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

  it('counts every property against the budget, including those with boolean schemas', () => {
    // Arrange: two nested arrays of 50 objects with 2000 `true` properties each would make about 5 million values.
    const wide = { type: 'object', properties: Object.fromEntries(Array.from({ length: 2000 }, (_, index) => [`p${index}`, true])) }
    const schema = { type: 'array', minItems: 50, items: { type: 'array', minItems: 50, items: wide } }
    const count = (value: unknown): number =>
      Array.isArray(value)
        ? value.reduce((sum: number, item) => sum + count(item), 1)
        : value && typeof value === 'object'
          ? Object.values(value).reduce((sum: number, item) => sum + count(item), 1)
          : 1

    // Act
    const example = exampleWithCheck(schema)!

    // Assert
    expect(count(example.value)).toBeLessThanOrEqual(1001)
    expect(example.needsEdits).toBe(true)
  })

  it('says an example cut short needs edits even when what is left satisfies the schema', () => {
    // Arrange: `null` is a valid value for every property, so only the cut says the example is incomplete.
    const schema = { type: 'object', properties: Object.fromEntries(Array.from({ length: 1500 }, (_, index) => [`p${index}`, { type: ['string', 'null'] }])) }

    // Act
    const example = exampleWithCheck(schema)!

    // Assert
    expect(Object.keys(example.value as object).length).toBeLessThan(1500)
    expect(conformsTo(example.value, schema)).toBe(true)
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
