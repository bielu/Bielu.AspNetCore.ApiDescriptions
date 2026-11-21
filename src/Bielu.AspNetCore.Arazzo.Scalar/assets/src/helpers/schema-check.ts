import { parseJsonPointer } from './resolve-target'

export type Schema = Record<string, unknown>

export const isSchema = (value: unknown): value is Schema => !!value && typeof value === 'object' && !Array.isArray(value)

/** Follows a local `$ref` against the schema's own root (`#/$defs/…`), then the document's (`#/components/inputs/…`). */
export const follow = (schema: Schema, roots: unknown[]): { schema: Schema; ref?: string } => {
  const ref = typeof schema.$ref === 'string' ? schema.$ref : undefined
  if (!ref) {
    return { schema }
  }
  const resolved = schema['$ref-value']
  if (isSchema(resolved)) {
    return { schema: { ...resolved, ...schema }, ref }
  }
  if (!ref.startsWith('#')) {
    return { schema, ref }
  }
  const segments = parseJsonPointer(ref.slice(1))
  for (const root of roots) {
    const target = segments.reduce<unknown>((node, segment) => (isSchema(node) ? node[segment] : undefined), root)
    if (isSchema(target)) {
      return { schema: { ...target, ...schema }, ref }
    }
  }
  return { schema, ref }
}

/** A schema nested deeper than this, without the value getting any deeper (`allOf` of a `$ref` to itself), is not checked further. */
const MAX_DEPTH = 32

const equal = (left: unknown, right: unknown): boolean => JSON.stringify(left) === JSON.stringify(right)

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

/** Whether `value` matches a schema `pattern`; a pattern this engine cannot read is not held against it. */
export const matchesPattern = (value: string, pattern: unknown): boolean => {
  if (typeof pattern !== 'string') {
    return true
  }
  try {
    return new RegExp(pattern, 'u').test(value)
  } catch {
    return true
  }
}

const check = (value: unknown, input: Schema, roots: unknown[], depth: number): boolean => {
  if (depth > MAX_DEPTH) {
    return true
  }
  const { schema } = follow(input, roots)
  const nested = (part: unknown, item: unknown = value) => !isSchema(part) || check(item, part, roots, depth + 1)

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
  if (Array.isArray(schema.allOf) && !schema.allOf.every((part) => nested(part))) {
    return false
  }
  if (Array.isArray(schema.anyOf) && schema.anyOf.length && !schema.anyOf.some((part) => nested(part))) {
    return false
  }
  if (Array.isArray(schema.oneOf) && schema.oneOf.length && schema.oneOf.filter((part) => nested(part)).length !== 1) {
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
      (schema.uniqueItems === true && new Set(value.map((item) => JSON.stringify(item))).size !== value.length) ||
      !value.every((item) => nested(schema.items, item))
    ) {
      return false
    }
  }
  if (isSchema(value)) {
    const properties = isSchema(schema.properties) ? schema.properties : {}
    const required = Array.isArray(schema.required) ? schema.required : []
    if (
      required.some((name) => typeof name === 'string' && !(name in value)) ||
      Object.entries(value).some(([name, item]) =>
        name in properties ? !nested(properties[name], item) : schema.additionalProperties === false || !nested(schema.additionalProperties, item),
      )
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
 * `additionalProperties`. `format` is an annotation and is not checked; a `$ref` that cannot be followed, or a
 * `pattern` this engine cannot read, is not held against the value. `roots` are where a local `$ref` is looked up.
 */
export const conformsTo = (value: unknown, schema: unknown, roots: unknown[] = [schema]): boolean => !isSchema(schema) || check(value, schema, roots, 0)
