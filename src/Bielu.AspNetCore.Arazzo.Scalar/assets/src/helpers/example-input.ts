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

const generate = (input: Schema, roots: unknown[], depth: number, seen: ReadonlySet<string>): unknown => {
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
    return generate(alternatives[0], roots, depth, nested)
  }
  if (Array.isArray(schema.allOf) && schema.allOf.every(isSchema)) {
    const own = isSchema(schema.properties) ? [generate({ ...schema, allOf: undefined }, roots, depth, nested)] : []
    const parts = [...schema.allOf.map((part) => generate(part as Schema, roots, depth, nested)), ...own]
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
        Object.entries(properties).map(([name, property]) => [name, isSchema(property) ? generate(property, roots, depth + 1, nested) : null]),
      )
    }
    case 'array': {
      const count = typeof schema.minItems === 'number' && schema.minItems > 1 ? Math.min(schema.minItems, 3) : 1
      return isSchema(schema.items) ? Array.from({ length: count }, () => generate(schema.items as Schema, roots, depth + 1, nested)) : []
    }
    case 'integer':
    case 'number': {
      const minimum =
        typeof schema.minimum === 'number' ? schema.minimum : typeof schema.exclusiveMinimum === 'number' ? schema.exclusiveMinimum + 1 : undefined
      return minimum ?? (typeof schema.maximum === 'number' && schema.maximum < 1 ? schema.maximum : 1)
    }
    case 'boolean':
      return true
    case 'null':
      return null
    case 'string': {
      const format = typeof schema.format === 'string' ? FORMAT_EXAMPLES[schema.format] : undefined
      if (format) {
        return format
      }
      const min = typeof schema.minLength === 'number' ? schema.minLength : 0
      return 'string'.padEnd(min, 'x')
    }
    default:
      return null
  }
}

/**
 * An example value for a JSON Schema, such as a workflow's `inputs`: what the schema says when it has an
 * `example`, `examples`, `const`, `default` or `enum`, else a value of its type (a sample for a known string
 * `format`, the minimum for a number). Objects list every property, arrays hold one item (or `minItems`, up to
 * three). `$ref`s are followed in the schema itself and then in `document` (the Arazzo document, for
 * `#/components/inputs/…`); a recursive reference ends in an empty object.
 *
 * `undefined` when there is no schema.
 */
export const exampleFromSchema = (schema: unknown, document?: unknown): unknown =>
  isSchema(schema) ? generate(schema, [schema, document], 0, new Set()) : undefined
