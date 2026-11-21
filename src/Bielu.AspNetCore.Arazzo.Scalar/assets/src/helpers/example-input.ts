import { parseJsonPointer } from './resolve-target'

type Schema = Record<string, unknown>

const isSchema = (value: unknown): value is Schema => !!value && typeof value === 'object' && !Array.isArray(value)

/** Nesting deeper than this gets no example; workflow inputs are far shallower. */
const MAX_DEPTH = 8

/** A value for a string in a known `format`, so the example reads like real input. */
const FORMAT_EXAMPLES: Record<string, string> = {
  uuid: '3fa85f64-5717-4562-b3fc-2c963f66afa6',
  'date-time': '2026-01-01T12:00:00Z',
  date: '2026-01-01',
  time: '12:00:00Z',
  email: 'user@example.com',
  uri: 'https://example.com',
  'uri-reference': '/path',
  hostname: 'example.com',
  ipv4: '192.0.2.1',
  ipv6: '2001:db8::1',
  duration: 'PT1H',
}

/** Follows a local `$ref` against the schema's own root (`#/$defs/…`), then the document's (`#/components/inputs/…`). */
const follow = (schema: Schema, roots: unknown[]): { schema: Schema; ref?: string } => {
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

/**
 * Which of several allowed types an example uses: the most specific. .NET writes a number it also reads from a
 * string as `["string", "integer"]` with a numeric `pattern`; the example should be the number.
 */
const TYPE_PREFERENCE = ['integer', 'number', 'boolean', 'object', 'array', 'string']

const typeOf = (schema: Schema): string | undefined => {
  if (Array.isArray(schema.type)) {
    const types = schema.type.filter((type): type is string => typeof type === 'string' && type !== 'null')
    return TYPE_PREFERENCE.find((type) => types.includes(type)) ?? types[0] ?? (schema.type.includes('null') ? 'null' : undefined)
  }
  if (typeof schema.type === 'string') {
    return schema.type
  }
  if (isSchema(schema.properties)) {
    return 'object'
  }
  if (isSchema(schema.items)) {
    return 'array'
  }
  return undefined
}

const generate = (input: Schema, roots: unknown[], depth: number, seen: ReadonlySet<string>, state: GenerationState): unknown => {
  const { schema, ref } = follow(input, roots)
  if (ref && seen.has(ref)) {
    // A recursive schema: stop rather than nest forever.
    return typeOf(schema) === 'array' ? [] : {}
  }
  const nested = ref ? new Set(seen).add(ref) : seen
  // What the author wrote wins over anything made up.
  if ('example' in schema && schema.example !== undefined) {
    return schema.example
  }
  if (Array.isArray(schema.examples) && schema.examples.length) {
    return schema.examples[0]
  }
  if ('const' in schema) {
    return schema.const
  }
  if ('default' in schema && schema.default !== undefined) {
    return schema.default
  }
  if (Array.isArray(schema.enum) && schema.enum.length) {
    return schema.enum[0]
  }
  const alternatives = [schema.oneOf, schema.anyOf].find((list): list is unknown[] => Array.isArray(list) && list.length > 0)
  if (alternatives && isSchema(alternatives[0])) {
    return generate(alternatives[0], roots, depth, nested, state)
  }
  if (Array.isArray(schema.allOf) && schema.allOf.every(isSchema)) {
    const own = isSchema(schema.properties) ? [generate({ ...schema, allOf: undefined }, roots, depth, nested, state)] : []
    const parts = [...schema.allOf.map((part) => generate(part as Schema, roots, depth, nested, state)), ...own]
    if (parts.every(isSchema)) {
      return Object.assign({}, ...parts)
    }
  }
  if (depth > MAX_DEPTH) {
    return null
  }
  switch (typeOf(schema)) {
    case 'object': {
      const properties = isSchema(schema.properties) ? schema.properties : {}
      return Object.fromEntries(
        Object.entries(properties).map(([name, property]) => [name, isSchema(property) ? generate(property, roots, depth + 1, nested, state) : null]),
      )
    }
    case 'array': {
      const count = typeof schema.minItems === 'number' && schema.minItems > 1 ? Math.min(schema.minItems, 3) : 1
      return isSchema(schema.items) ? Array.from({ length: count }, () => generate(schema.items as Schema, roots, depth + 1, nested, state)) : []
    }
    case 'integer':
    case 'number': {
      const minimum =
        typeof schema.minimum === 'number' ? schema.minimum : typeof schema.exclusiveMinimum === 'number' ? schema.exclusiveMinimum + 1 : undefined
      const value = minimum ?? (typeof schema.maximum === 'number' && schema.maximum < 1 ? schema.maximum : 1)
      if (
        (typeof schema.maximum === 'number' && value > schema.maximum) ||
        (typeof schema.exclusiveMaximum === 'number' && value >= schema.exclusiveMaximum)
      ) {
        state.needsEdits = true
      }
      return value
    }
    case 'boolean':
      return true
    case 'null':
      return null
    case 'string':
      return generateString(schema, state)
    default:
      return null
  }
}

/** Whether `value` matches a schema `pattern`; a pattern this engine cannot read is not held against it. */
const matchesPattern = (value: string, pattern: unknown): boolean => {
  if (typeof pattern !== 'string') {
    return true
  }
  try {
    return new RegExp(pattern, 'u').test(value)
  } catch {
    return true
  }
}

/**
 * A string within `minLength` and `maxLength`: the sample for its `format`, else `string` padded or cut to fit.
 * A format sample that does not fit, or a value that misses the `pattern`, marks the example as needing edits.
 */
const generateString = (schema: Schema, state: GenerationState): string => {
  const min = typeof schema.minLength === 'number' ? schema.minLength : 0
  const max = typeof schema.maxLength === 'number' ? schema.maxLength : Number.POSITIVE_INFINITY
  const sample = typeof schema.format === 'string' ? FORMAT_EXAMPLES[schema.format] : undefined
  const value = sample ?? 'string'.padEnd(min, 'x').slice(0, max)
  if (value.length < min || value.length > max || !matchesPattern(value, schema.pattern)) {
    state.needsEdits = true
  }
  return value
}

/** What generating one example found out on the way. */
type GenerationState = {
  /** Set when a value could not be made to satisfy its schema (a `pattern`, bounds a sample cannot meet). */
  needsEdits: boolean
}

/** An example made from a schema, and whether some of its values do not satisfy the schema yet. */
export type SchemaExample = { value: unknown; needsEdits: boolean }

/**
 * {@link exampleFromSchema}, also saying whether the example needs edits before use: `needsEdits` when a
 * value could not be made to satisfy its schema, such as a string `pattern` or a `format` whose sample is
 * longer than `maxLength`. `undefined` when there is no schema.
 */
export const exampleWithCheck = (schema: unknown, document?: unknown): SchemaExample | undefined => {
  if (!isSchema(schema)) {
    return undefined
  }
  const state: GenerationState = { needsEdits: false }
  const value = generate(schema, [schema, document], 0, new Set(), state)
  return { value, needsEdits: state.needsEdits }
}

/**
 * An example value for a JSON Schema, such as a workflow's `inputs`: what the schema says when it has an
 * `example`, `examples`, `const`, `default` or `enum`, else a value of its type (a sample for a known string
 * `format`, the minimum for a number, a string within its length bounds). Objects list every property, arrays
 * hold one item (or `minItems`, up to three). `$ref`s are followed in the schema itself and then in `document`
 * (the Arazzo document, for `#/components/inputs/…`); a recursive reference ends in an empty object.
 *
 * `undefined` when there is no schema.
 */
export const exampleFromSchema = (schema: unknown, document?: unknown): unknown => exampleWithCheck(schema, document)?.value
