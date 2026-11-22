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

/** Whether the pattern repeats at `index`: `*`, `+` or an open-ended or ranged `{n,…}`. */
const repeatsAt = (pattern: string, index: number): boolean =>
  pattern[index] === '*' || pattern[index] === '+' || (pattern[index] === '{' && /^\{\d+,/.test(pattern.slice(index, index + 16)))

/**
 * Whether a pattern repeats a group that itself repeats or has alternatives (`(a*)*`, `(a|aa)+`): the shapes
 * that make a backtracking engine take exponential time on a near miss. A heuristic: it also flags some safe
 * patterns (`(cat|dog)+`), which only means they are not checked.
 */
export const mayBacktrack = (pattern: string): boolean => {
  const groups = [{ repeats: false, alternatives: false }]
  for (let index = 0; index < pattern.length; index++) {
    const character = pattern[index]
    if (character === '\\') {
      index++
    } else if (character === '[') {
      for (index++; index < pattern.length && pattern[index] !== ']'; index++) {
        if (pattern[index] === '\\') {
          index++
        }
      }
    } else if (character === '(') {
      groups.push({ repeats: false, alternatives: false })
    } else if (character === '|') {
      groups[groups.length - 1]!.alternatives = true
    } else if (character === ')') {
      const group = groups.length > 1 ? groups.pop()! : { repeats: false, alternatives: false }
      const repeated = repeatsAt(pattern, index + 1)
      if (repeated && (group.repeats || group.alternatives)) {
        return true
      }
      groups[groups.length - 1]!.repeats ||= repeated || group.repeats
    } else if (repeatsAt(pattern, index)) {
      groups[groups.length - 1]!.repeats = true
    }
  }
  return false
}

/** Values longer than this are not matched against a `pattern`: even a polynomial pattern gets slow on them. */
const MAX_PATTERN_INPUT = 1000

/**
 * Whether `value` matches a schema `pattern`. The match runs on the render thread, so a pattern this engine
 * cannot read, one that may backtrack catastrophically ({@link mayBacktrack}), or a very long value is not
 * held against the value.
 */
export const matchesPattern = (value: string, pattern: unknown): boolean => {
  if (typeof pattern !== 'string' || value.length > MAX_PATTERN_INPUT || mayBacktrack(pattern)) {
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
 * round it again says nothing more.
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
  if (ref) {
    if (refs.has(ref)) {
      return true
    }
    // Both apply: the schema referred to, in full, and the keywords written next to the `$ref`.
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
