import { conformsTo, follow, isSchema, type Schema } from './schema-check'

/** Nesting deeper than this gets no example; workflow inputs are far shallower. */
const MAX_DEPTH = 8

/** An array example holds at most this many items, whatever `minItems` asks for. */
const MAX_ITEMS = 50

/**
 * A whole example makes at most this many values. Nested arrays multiply (six levels of `minItems: 50` ask for
 * 50⁶ values), and the example is made while the workflow renders, so past this the rest is left out, or `null`,
 * and the example is marked as needing edits.
 */
const MAX_VALUES = 1000

/** A made-up string is at most this long, whatever `minLength` asks for; a longer bound marks the example as needing edits. */
const MAX_STRING_LENGTH = 1000

/**
 * The example's JSON is at most about this many characters. An authored `example` counts its own size, so one
 * large value repeated by `minItems` cannot make the example huge; a value that does not fit is left `null`.
 */
const MAX_EXAMPLE_LENGTH = 100_000

/**
 * How many values, and how many characters of JSON, the example being made may still take, and whether anything
 * was left out or cut short to stay within the limits: then the example needs edits whether or not the shorter
 * one happens to satisfy the schema (a `null` left in place of a `["string", "null"]` value does).
 */
type Budget = { left: number; length: number; cut: boolean }

/**
 * How long `text` is as a JSON string, quotes and escapes included (`"` and `\` take two characters, control
 * characters up to six). A string longer than `limit` is past it whatever its escapes, so it is not serialized.
 */
const quotedLength = (text: string, limit: number): number => (text.length > limit ? text.length + 2 : JSON.stringify(text).length)

/** Takes `length` characters of JSON from the budget; `false`, and the example marked as cut, when they do not fit. */
const take = (budget: Budget, length: number): boolean => {
  if (length > budget.length) {
    budget.cut = true
    return false
  }
  budget.length -= length
  return true
}

/** About how long `value` is as JSON, counting no further than `limit`: past it, more than `limit`. */
const jsonLength = (value: unknown, limit: number): number => {
  let total = 0
  const visit = (node: unknown): void => {
    if (total > limit) {
      return
    }
    if (typeof node === 'string') {
      total += quotedLength(node, limit)
    } else if (Array.isArray(node)) {
      total += 2
      for (const item of node) {
        total += 1
        visit(item)
        if (total > limit) {
          return
        }
      }
    } else if (node && typeof node === 'object') {
      total += 2
      for (const [key, item] of Object.entries(node)) {
        total += quotedLength(key, limit) + 2
        visit(item)
        if (total > limit) {
          return
        }
      }
    } else {
      total += String(node).length
    }
  }
  visit(value)
  return total
}

/** `value` when its JSON fits in what the example has left (taking that much), else `null`, and the example marked as cut. */
const fit = <T>(budget: Budget, value: T): T | null => (take(budget, jsonLength(value, budget.length)) ? value : null)

/** Takes one value from the budget; `false`, and the example marked as cut, when there is none left. */
const spend = (budget: Budget): boolean => {
  if (budget.left <= 0) {
    budget.cut = true
    return false
  }
  budget.left--
  return true
}

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

const generate = (input: Schema, roots: unknown[], depth: number, seen: ReadonlySet<string>, budget: Budget): unknown => {
  if (!spend(budget)) {
    return null
  }
  const { schema, ref } = follow(input, roots)
  if (ref && seen.has(ref)) {
    // A recursive schema: stop rather than nest forever.
    return typeOf(schema) === 'array' ? [] : {}
  }
  const nested = ref ? new Set(seen).add(ref) : seen
  // What the author wrote wins over anything made up, as long as it fits.
  if ('example' in schema && schema.example !== undefined) {
    return fit(budget, schema.example)
  }
  if (Array.isArray(schema.examples) && schema.examples.length) {
    return fit(budget, schema.examples[0])
  }
  if ('const' in schema) {
    return fit(budget, schema.const)
  }
  if ('default' in schema && schema.default !== undefined) {
    return fit(budget, schema.default)
  }
  if (Array.isArray(schema.enum) && schema.enum.length) {
    return fit(budget, schema.enum[0])
  }
  const alternatives = [schema.oneOf, schema.anyOf].find((list): list is unknown[] => Array.isArray(list) && list.length > 0)
  if (alternatives && isSchema(alternatives[0])) {
    return generate(alternatives[0], roots, depth, nested, budget)
  }
  if (Array.isArray(schema.allOf) && schema.allOf.every(isSchema)) {
    const own = isSchema(schema.properties) ? [generate({ ...schema, allOf: undefined }, roots, depth, nested, budget)] : []
    const parts = [...schema.allOf.map((part) => generate(part as Schema, roots, depth, nested, budget)), ...own]
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
      const value: Record<string, unknown> = {}
      for (const [name, property] of Object.entries(properties)) {
        // A property whose schema is `false` must be absent.
        if (property === false) {
          continue
        }
        // The name is written once per copy of the object, so it counts too: `"name": `, escapes included.
        if (!take(budget, quotedLength(name, budget.length) + 2)) {
          break
        }
        if (isSchema(property)) {
          value[name] = generate(property, roots, depth + 1, nested, budget)
        } else if (spend(budget)) {
          value[name] = null
        }
        // Every property counts, so once the budget is spent the rest are left out.
        if (budget.cut) {
          break
        }
      }
      return value
    }
    case 'array': {
      // One item, or as many as minItems asks for, but no more than maxItems allows.
      const min = typeof schema.minItems === 'number' ? schema.minItems : 0
      const max = typeof schema.maxItems === 'number' ? schema.maxItems : Number.POSITIVE_INFINITY
      const count = Math.min(Math.max(1, min), max, MAX_ITEMS)
      const items: unknown[] = []
      for (let index = 0; isSchema(schema.items) && index < count && !budget.cut; index++) {
        items.push(generate(schema.items, roots, depth + 1, nested, budget))
      }
      return items
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
      return fit(budget, generateString(schema, budget))
    default:
      return null
  }
}

/** A string within `minLength` and `maxLength`: the sample for its `format`, else `string` padded or cut to fit. */
const generateString = (schema: Schema, budget: Budget): string => {
  const min = typeof schema.minLength === 'number' ? schema.minLength : 0
  const max = typeof schema.maxLength === 'number' ? schema.maxLength : Number.POSITIVE_INFINITY
  const sample = typeof schema.format === 'string' ? FORMAT_EXAMPLES[schema.format] : undefined
  if (sample === undefined && min > MAX_STRING_LENGTH) {
    budget.cut = true
  }
  return sample ?? 'string'.padEnd(Math.min(min, MAX_STRING_LENGTH), 'x').slice(0, max)
}

/** An example made from a schema, and whether some of its values do not satisfy the schema yet. */
export type SchemaExample = { value: unknown; needsEdits: boolean }

/**
 * {@link exampleFromSchema}, also saying whether the example needs edits before use: the example, made up or
 * authored, is checked against the schema ({@link conformsTo}), and `needsEdits` is set when it does not
 * satisfy it, such as a string `pattern`, a `format` sample longer than `maxLength` or a `minItems` above
 * `maxItems`, and also when the example was cut short to stay within its limits (at most 1000 values, strings
 * of at most 1000 characters, and about 100,000 characters of JSON, authored values included). `undefined` when there is no schema.
 */
export const exampleWithCheck = (schema: unknown, document?: unknown): SchemaExample | undefined => {
  if (!isSchema(schema)) {
    return undefined
  }
  const roots = [schema, document]
  const budget: Budget = { left: MAX_VALUES, length: MAX_EXAMPLE_LENGTH, cut: false }
  const value = generate(schema, roots, 0, new Set(), budget)
  return { value, needsEdits: budget.cut || !conformsTo(value, schema, roots) }
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
