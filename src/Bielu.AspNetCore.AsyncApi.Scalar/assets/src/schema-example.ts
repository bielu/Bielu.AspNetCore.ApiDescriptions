import { deref } from './documents'

const MAX_SCHEMA_DEPTH = 6

type AnyRecord = Record<string, any>

// Numeric `format`s that some schema generators emit with `type: "string"` (e.g. .NET renders
// `int` as `{ type: "string", format: "int32" }`). Treated as numbers so examples stay invocable.
const NUMERIC_FORMATS = new Set(['int32', 'int64', 'integer', 'long', 'double', 'float', 'decimal', 'number'])

/** A placeholder value for a `string` schema, honouring common formats. */
function exampleString(schema: AnyRecord): string {
  switch (schema.format) {
    case 'date-time':
      return new Date().toISOString()
    case 'date':
      return new Date().toISOString().slice(0, 10)
    case 'time':
      return new Date().toISOString().slice(11, 19)
    case 'uuid':
    case 'guid':
      return '00000000-0000-0000-0000-000000000000'
    case 'email':
      return 'user@example.com'
    case 'uri':
    case 'url':
      return 'https://example.com'
    default:
      return 'string'
  }
}

/** Build a representative example value from a JSON schema node. */
export function exampleFromSchema(
  doc: AnyRecord,
  node: AnyRecord | undefined,
  depth = 0,
  seen = new Set<string>(),
): unknown {
  const schema = deref(doc, node, new Set(seen))
  if (!schema || depth > MAX_SCHEMA_DEPTH) {
    return null
  }

  // AsyncAPI multi-format payloads wrap the JSON schema under `schema`.
  if (schema.schema && !schema.type && !schema.properties && !schema.enum) {
    return exampleFromSchema(doc, schema.schema, depth, seen)
  }

  // Prefer explicit, document-provided values.
  if (Array.isArray(schema.examples) && schema.examples.length > 0) {
    return schema.examples[0]
  }
  if (schema.example !== undefined) {
    return schema.example
  }
  if (schema.default !== undefined) {
    return schema.default
  }
  if (Array.isArray(schema.enum) && schema.enum.length > 0) {
    return schema.enum[0]
  }
  if (schema.const !== undefined) {
    return schema.const
  }

  const composite: AnyRecord[] | undefined = schema.allOf ?? schema.oneOf ?? schema.anyOf
  if (Array.isArray(composite) && composite.length > 0) {
    if (schema.allOf) {
      const merged: AnyRecord = {}
      for (const sub of composite) {
        const value = exampleFromSchema(doc, sub, depth, seen)
        if (value && typeof value === 'object' && !Array.isArray(value)) {
          Object.assign(merged, value)
        }
      }
      if (Object.keys(merged).length > 0) {
        return merged
      }
    }
    return exampleFromSchema(doc, composite[0], depth, seen)
  }

  const type = Array.isArray(schema.type) ? schema.type.find((t: string) => t !== 'null') : schema.type

  // A numeric format wins over a (sometimes incorrect) `string` type.
  if (typeof schema.format === 'string' && NUMERIC_FORMATS.has(schema.format)) {
    return typeof schema.minimum === 'number' ? schema.minimum : 0
  }

  switch (type) {
    case 'object':
      return exampleObject(doc, schema, depth, seen)
    case 'array': {
      const item = exampleFromSchema(doc, schema.items, depth + 1, seen)
      return item === null ? [] : [item]
    }
    case 'string':
      return exampleString(schema)
    case 'integer':
    case 'number':
      return typeof schema.minimum === 'number' ? schema.minimum : 0
    case 'boolean':
      return false
    case 'null':
      return null
    default:
      return schema.properties ? exampleObject(doc, schema, depth, seen) : null
  }
}

function exampleObject(doc: AnyRecord, schema: AnyRecord, depth: number, seen: Set<string>): AnyRecord {
  const out: AnyRecord = {}
  const properties: AnyRecord = schema.properties ?? {}
  for (const key of Object.keys(properties)) {
    out[key] = exampleFromSchema(doc, properties[key], depth + 1, seen)
  }
  return out
}
