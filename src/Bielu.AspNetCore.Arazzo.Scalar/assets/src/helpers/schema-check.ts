import { parseJsonPointer } from './resolve-target'

export type Schema = Record<string, unknown>

export const isSchema = (value: unknown): value is Schema => !!value && typeof value === 'object' && !Array.isArray(value)

/** An array index in a JSON Pointer: digits, without leading zeros (RFC 6901). */
const ARRAY_INDEX = /^(?:0|[1-9]\d*)$/

/** The node a JSON Pointer names, through objects and arrays (`#/properties/a/oneOf/0`). */
const pointTo = (root: unknown, segments: string[]): unknown =>
  segments.reduce<unknown>(
    (node, segment) => (Array.isArray(node) ? (ARRAY_INDEX.test(segment) ? node[Number(segment)] : undefined) : isSchema(node) ? node[segment] : undefined),
    root,
  )

/**
 * The schema a `$ref` names, which may be a boolean schema: its resolved `$ref-value`, else the local pointer
 * looked up against the schema's own root (`#/$defs/…`), then the document's (`#/components/inputs/…`).
 * `undefined` when there is no `$ref`, or it cannot be followed.
 */
export const refTarget = (schema: Schema, roots: unknown[]): Schema | boolean | undefined => {
  const resolved = schema['$ref-value']
  if (isSchema(resolved) || typeof resolved === 'boolean') {
    return resolved
  }
  const ref = schema.$ref
  if (typeof ref !== 'string' || !ref.startsWith('#')) {
    return undefined
  }
  const segments = parseJsonPointer(ref.slice(1))
  for (const root of roots) {
    const target = pointTo(root, segments)
    if (isSchema(target) || typeof target === 'boolean') {
      return target
    }
  }
  return undefined
}

const withoutRef = (schema: Schema): Schema => {
  const { $ref: _ref, '$ref-value': _value, ...rest } = schema
  return rest
}

/**
 * A schema with its `$ref` followed, through every reference it leads to (a cycle ends the chain), merged into
 * one for making up a value: the keywords written next to a `$ref` win over those of what it refers to. Only
 * for generating an example; checking a value applies each schema of the chain in full ({@link conformsTo}).
 */
export const follow = (schema: Schema, roots: unknown[]): { schema: Schema; ref?: string } => {
  const ref = typeof schema.$ref === 'string' ? schema.$ref : undefined
  if (!ref) {
    return { schema }
  }
  const layers: Schema[] = [schema]
  const seen = new Set([ref])
  for (let current = schema; ; ) {
    const target = refTarget(current, roots)
    if (!isSchema(target)) {
      break
    }
    layers.push(target)
    const next = typeof target.$ref === 'string' ? target.$ref : undefined
    if (!next || seen.has(next)) {
      break
    }
    seen.add(next)
    current = target
  }
  return layers.length === 1 ? { schema, ref } : { schema: Object.assign({}, ...layers.reverse().map(withoutRef)), ref }
}

/** A schema nested deeper than this, without the value getting any deeper (`allOf` of a `$ref` to itself), is not checked further. */
const MAX_DEPTH = 32

/** JSON with every object's keys in order, so two values equal in JSON Schema's sense serialize the same. */
const canonical = (value: unknown): unknown =>
  Array.isArray(value)
    ? value.map(canonical)
    : isSchema(value)
      ? Object.fromEntries(
          Object.keys(value)
            .sort()
            .map((key) => [key, canonical(value[key])]),
        )
      : value

const jsonKey = (value: unknown): string => JSON.stringify(canonical(value))

/** JSON Schema equality: object properties in any order, array items in order. */
const equal = (left: unknown, right: unknown): boolean => jsonKey(left) === jsonKey(right)

const hasType = (value: unknown, type: unknown): boolean => {
  switch (type) {
    case 'string':
      return typeof value === 'string'
    case 'integer':
      return Number.isInteger(value)
    case 'number':
      return typeof value === 'number' && Number.isFinite(value)
    case 'boolean':
      return typeof value === 'boolean'
    case 'null':
      return value === null
    case 'array':
      return Array.isArray(value)
    case 'object':
      return isSchema(value)
    default:
      return true
  }
}

/** How a quantifier repeats what it follows: a variable number of times, zero or once, or a fixed number. */
type Quantifier = { kind: 'repeat' | 'optional' | 'fixed' | 'once'; length: number }

/** The quantifier at `index` (`*`, `+`, `?`, `{n}`, `{n,}`, `{n,m}`, each maybe lazy), if there is one. */
const quantifierAt = (pattern: string, index: number): Quantifier | undefined => {
  const character = pattern[index]
  let quantifier: Quantifier | undefined
  if (character === '*' || character === '+') {
    quantifier = { kind: 'repeat', length: 1 }
  } else if (character === '?') {
    quantifier = { kind: 'optional', length: 1 }
  } else if (character === '{') {
    const match = /^\{(\d+)(,(\d*))?\}/.exec(pattern.slice(index, index + 24))
    if (!match) {
      return undefined
    }
    const min = Number(match[1])
    const max = match[2] === undefined ? min : match[3] === '' ? Number.POSITIVE_INFINITY : Number(match[3])
    quantifier = { kind: max > min ? 'repeat' : min > 1 ? 'fixed' : 'once', length: match[0].length }
  }
  if (quantifier && pattern[index + quantifier.length] === '?') {
    quantifier.length++
  }
  return quantifier
}

/**
 * What bounds how long a backtracking engine can take on a pattern: how many quantifiers repeat a variable
 * number of times, how many are optional, the alternatives it chooses between, and the two shapes no input
 * length bounds: a group repeated (or repeated a fixed number of times) with any choice inside it, at any
 * depth: a repeat, an optional part or alternatives (`(a*)*`, `(a|aa)+`, `(a+){9}`, `(a?){30}`, `((a|b)c)+`),
 * whose choices multiply with each repetition; and backreferences (`\1`, `\k<name>`).
 */
export type PatternShape = { repeats: number; optionals: number; choices: number; unbounded: boolean }

export const patternShape = (pattern: string): PatternShape => {
  const shape: PatternShape = { repeats: 0, optionals: 0, choices: 1, unbounded: false }
  /** `varies`: the group, or a group inside it, repeats, has an optional part or has alternatives. */
  type Group = { varies: boolean; branches: number }
  const groups: Group[] = [{ varies: false, branches: 1 }]
  const current = () => groups[groups.length - 1]!
  let closed: Group | undefined
  for (let index = 0; index < pattern.length; index++) {
    const character = pattern[index]!
    const quantifier = index > 0 ? quantifierAt(pattern, index) : undefined
    if (quantifier) {
      if (quantifier.kind === 'repeat') {
        shape.repeats++
        current().varies = true
      } else if (quantifier.kind === 'optional') {
        shape.optionals++
        current().varies = true
      }
      if (closed && (quantifier.kind === 'repeat' || quantifier.kind === 'fixed') && (closed.varies || closed.branches > 1)) {
        shape.unbounded = true
      }
      index += quantifier.length - 1
      closed = undefined
      continue
    }
    closed = undefined
    if (character === '\\') {
      if (/[1-9k]/.test(pattern[index + 1] ?? '')) {
        shape.unbounded = true
      }
      index++
    } else if (character === '[') {
      for (index++; index < pattern.length && pattern[index] !== ']'; index++) {
        if (pattern[index] === '\\') {
          index++
        }
      }
    } else if (character === '(') {
      groups.push({ varies: false, branches: 1 })
      // `(?:`, `(?=`, `(?!`, `(?<=`, `(?<!`, `(?<name>`: the `?` is not a quantifier.
      const prefix = /^\(\?(?:[:=!]|<[=!]|<[^>]*>)/.exec(pattern.slice(index, index + 64))
      index += prefix ? prefix[0].length - 1 : 0
    } else if (character === '|') {
      current().branches++
    } else if (character === ')') {
      const group = groups.length > 1 ? groups.pop()! : { varies: false, branches: 1 }
      shape.choices *= group.branches
      current().varies ||= group.varies || group.branches > 1
      closed = group
    }
  }
  shape.choices *= groups[0]!.branches
  return shape
}

/** The most attempts a pattern may need on a value for it to be run. */
const MAX_PATTERN_STEPS = 10_000_000

/** Values longer than this are not matched against a `pattern`, whatever its shape. */
const MAX_PATTERN_INPUT = 1000

/**
 * Whether a backtracking engine is bound to match `pattern` against a value of `length` characters quickly.
 * Without unbounded shapes ({@link PatternShape}), each of the `repeats` quantifiers takes one of at most
 * `length + 1` counts at each of `length + 1` start positions, each optional part is in or out, and each set of
 * alternatives picks one, so the attempts are at most `(length + 1)^(repeats + 1) · 2^optionals · choices`.
 * So `^\d+(\.\d+)?$` (two repeats, one optional part) is run on values of up to 169 characters, while
 * `^a*a*a*a*a*a*a*a*b$` is run on none but the shortest.
 */
export const matchesQuickly = (pattern: string, length: number): boolean => {
  if (length > MAX_PATTERN_INPUT) {
    return false
  }
  const shape = patternShape(pattern)
  return !shape.unbounded && (length + 1) ** (shape.repeats + 1) * 2 ** shape.optionals * shape.choices <= MAX_PATTERN_STEPS
}

/**
 * Whether `value` matches a schema `pattern`. The match runs on the render thread and cannot be interrupted, so
 * it is only run when it is bound to be quick ({@link matchesQuickly}); otherwise, as for a pattern this engine
 * cannot read, the pattern is not held against the value.
 */
export const matchesPattern = (value: string, pattern: unknown): boolean => {
  if (typeof pattern !== 'string' || !matchesQuickly(pattern, value.length)) {
    return true
  }
  try {
    return new RegExp(pattern, 'u').test(value)
  } catch {
    return true
  }
}

/**
 * `refs` are the references already followed for this same value, so a cycle (`a` → `b` → `a`) ends: going
 * round it again would only check the same schemas again.
 */
const check = (value: unknown, input: unknown, roots: unknown[], depth: number, refs: ReadonlySet<string> = new Set()): boolean => {
  if (typeof input === 'boolean') {
    // `true` allows every value, `false` none.
    return input
  }
  if (!isSchema(input) || depth > MAX_DEPTH) {
    return true
  }
  const ref = typeof input.$ref === 'string' ? input.$ref : undefined
  // Both apply: the schema referred to, in full, and the keywords written next to the `$ref`. A reference
  // already followed for this value is not followed again, but its siblings still are checked below.
  if (ref && !refs.has(ref)) {
    const target = refTarget(input, roots)
    if (target !== undefined && !check(value, target, roots, depth + 1, new Set(refs).add(ref))) {
      return false
    }
  }
  const schema = ref ? withoutRef(input) : input
  /** A subschema applied to the same value (`allOf`, …). */
  const same = (part: unknown) => check(value, part, roots, depth + 1, refs)
  /** A subschema applied to an item or a property of the value. */
  const child = (part: unknown, item: unknown) => check(item, part, roots, depth + 1)

  if (schema.type !== undefined) {
    const types = Array.isArray(schema.type) ? schema.type : [schema.type]
    if (!types.some((type) => hasType(value, type))) {
      return false
    }
  }
  if (Array.isArray(schema.enum) && !schema.enum.some((option) => equal(option, value))) {
    return false
  }
  if ('const' in schema && !equal(schema.const, value)) {
    return false
  }
  if (Array.isArray(schema.allOf) && !schema.allOf.every(same)) {
    return false
  }
  if (Array.isArray(schema.anyOf) && schema.anyOf.length && !schema.anyOf.some(same)) {
    return false
  }
  if (Array.isArray(schema.oneOf) && schema.oneOf.length && schema.oneOf.filter(same).length !== 1) {
    return false
  }

  if (typeof value === 'string') {
    const length = [...value].length
    if (
      (typeof schema.minLength === 'number' && length < schema.minLength) ||
      (typeof schema.maxLength === 'number' && length > schema.maxLength) ||
      !matchesPattern(value, schema.pattern)
    ) {
      return false
    }
  }
  if (typeof value === 'number') {
    if (
      (typeof schema.minimum === 'number' && value < schema.minimum) ||
      (typeof schema.maximum === 'number' && value > schema.maximum) ||
      (typeof schema.exclusiveMinimum === 'number' && value <= schema.exclusiveMinimum) ||
      (typeof schema.exclusiveMaximum === 'number' && value >= schema.exclusiveMaximum) ||
      (typeof schema.multipleOf === 'number' && schema.multipleOf > 0 && !Number.isInteger(value / schema.multipleOf))
    ) {
      return false
    }
  }
  if (Array.isArray(value)) {
    if (
      (typeof schema.minItems === 'number' && value.length < schema.minItems) ||
      (typeof schema.maxItems === 'number' && value.length > schema.maxItems) ||
      (schema.uniqueItems === true && new Set(value.map(jsonKey)).size !== value.length) ||
      !value.every((item) => child(schema.items, item))
    ) {
      return false
    }
  }
  if (isSchema(value)) {
    const properties = isSchema(schema.properties) ? schema.properties : {}
    const required = Array.isArray(schema.required) ? schema.required : []
    if (
      required.some((name) => typeof name === 'string' && !(name in value)) ||
      Object.entries(value).some(([name, item]) => !child(name in properties ? properties[name] : schema.additionalProperties, item))
    ) {
      return false
    }
  }
  return true
}

/**
 * Whether `value` satisfies `schema`, for the JSON Schema keywords an example input can be checked against
 * without a full validator: `type`, `enum`, `const`, `allOf`/`anyOf`/`oneOf`, string length and `pattern`,
 * number bounds and `multipleOf`, array length, `items` and `uniqueItems`, `required`, `properties` and
 * `additionalProperties`, and the boolean schemas `true` and `false`. A `$ref` applies the schema it refers to
 * together with the keywords next to it. `format` is an annotation and is not checked; a `$ref` that cannot be
 * followed, or a `pattern` this engine cannot read or should not run ({@link matchesPattern}), is not held
 * against the value. `roots` are where a local `$ref` is looked up.
 */
export const conformsTo = (value: unknown, schema: unknown, roots: unknown[] = [schema]): boolean => check(value, schema, roots, 0)
