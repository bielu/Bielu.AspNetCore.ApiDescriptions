import { conformsTo, follow, isSchema, type Schema } from './schema-check'

/** Nesting deeper than this gets no example; workflow inputs are far shallower. */
const MAX_DEPTH = 8

/** An array example holds at most this many items, whatever `minItems` asks for. */
const MAX_ITEMS = 50

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
      // One item, or as many as minItems asks for, but no more than maxItems allows.
      const min = typeof schema.minItems === 'number' ? schema.minItems : 0
      const max = typeof schema.maxItems === 'number' ? schema.maxItems : Number.POSITIVE_INFINITY
      const count = Math.min(Math.max(1, min), max, MAX_ITEMS)
      return isSchema(schema.items) ? Array.from({ length: count }, () => generate(schema.items as Schema, roots, depth + 1, nested)) : []
    }
    case 'integer':
    case 'number': {
      const minimum =
        typeof schema.minimum === 'number' ? schema.minimum : typeof schema.exclusiveMinimum === 'number' ? schema.exclusiveMinimum + 1 : undefined
      const value = minimum ?? (typeof schema.maximum === 'number' && schema.maximum < 1 ? schema.maximum : 1)
      // The first multiple at or above it, so { minimum: 3, multipleOf: 5 } gives 5.
      return typeof schema.multipleOf === 'number' && schema.multipleOf > 0 ? Math.ceil(value / schema.multipleOf) * schema.multipleOf : value
    }
    case 'boolean':
      return true
    case 'null':
      return null
    case 'string':
      return generateString(schema)
    default:
      return null
  }
}

/** A string within `minLength` and `maxLength`: the sample for its `format`, else `string` padded or cut to fit. */
const generateString = (schema: Schema): string => {
  const min = typeof schema.minLength === 'number' ? schema.minLength : 0
  const max = typeof schema.maxLength === 'number' ? schema.maxLength : Number.POSITIVE_INFINITY
  const sample = typeof schema.format === 'string' ? FORMAT_EXAMPLES[schema.format] : undefined
  return sample ?? 'string'.padEnd(min, 'x').slice(0, max)
}

/** An example made from a schema, and whether some of its values do not satisfy the schema yet. */
export type SchemaExample = { value: unknown; needsEdits: boolean }

/**
 * {@link exampleFromSchema}, also saying whether the example needs edits before use: the example, made up or
 * authored, is checked against the schema ({@link conformsTo}), and `needsEdits` is set when it does not
 * satisfy it, such as a string `pattern`, a `format` sample longer than `maxLength` or a `minItems` above
 * `maxItems`. `undefined` when there is no schema.
 */
export const exampleWithCheck = (schema: unknown, document?: unknown): SchemaExample | undefined => {
  if (!isSchema(schema)) {
    return undefined
  }
  const roots = [schema, document]
  const value = generate(schema, roots, 0, new Set())
  return { value, needsEdits: !conformsTo(value, schema, roots) }
}

/**
 * An example value for a JSON Schema, such as a workflow's `inputs`: what the schema says when it has an
 * `example`, `examples`, `const`, `default` or `enum`, else a value of its type (a sample for a known string
 * `format`, the minimum for a number, a string within its length bounds). Objects list every property, arrays
 * hold one item, or `minItems` (within `maxItems`). `$ref`s are followed in the schema itself and then in `document`
 * (the Arazzo document, for `#/components/inputs/…`); a recursive reference ends in an empty object.
 *
 * `undefined` when there is no schema.
 */
export const exampleFromSchema = (schema: unknown, document?: unknown): unknown => exampleWithCheck(schema, document)?.value
