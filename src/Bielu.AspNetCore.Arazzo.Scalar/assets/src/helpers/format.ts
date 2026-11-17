import type { ArazzoCriterion, ArazzoSelector } from '../types'
import { parseJsonPointer } from './resolve-target'

/** Shows a constant, Runtime Expression or Selector Object as text: strings verbatim, the rest as JSON. */
export const formatValue = (value: unknown): string => {
  if (typeof value === 'string') {
    return value
  }
  if (value === undefined) {
    return ''
  }
  return JSON.stringify(value, null, 2)
}

/** One property of a workflow's `inputs` JSON Schema, with its nested properties as `children`. */
export type SchemaPropertyRow = {
  name: string
  type: string
  required: boolean
  description: string
  /** Short facts worth a glance: `format`, allowed values, default, bounds. Omitted when there are none. */
  details?: string[]
  /** The properties of an object property, or of an array property's items. */
  children?: SchemaPropertyRow[]
}

type Schema = Record<string, unknown>

/** Nesting deeper than this is shown no further; real workflow inputs are far shallower. */
const MAX_DEPTH = 6

const isSchema = (value: unknown): value is Schema => !!value && typeof value === 'object' && !Array.isArray(value)

const typeName = (type: unknown): string | undefined =>
  Array.isArray(type) ? type.join(' | ') : typeof type === 'string' ? type : undefined

/**
 * Follows a `$ref` that points inside the same schema (`#/$defs/…`, the form `JsonSchemaExporter`
 * writes). Returns the referenced schema, or `undefined` for a reference it cannot follow.
 */
const followRef = (root: Schema, ref: string): Schema | undefined => {
  if (!ref.startsWith('#')) {
    return undefined
  }
  const target = parseJsonPointer(ref.slice(1)).reduce<unknown>(
    (node, segment) => (isSchema(node) ? node[segment] : undefined),
    root,
  )
  return isSchema(target) ? target : undefined
}

/** The schema a property describes: itself, or what its `$ref` points to, with the property's own keywords on top. */
const resolveSchema = (root: Schema, schema: Schema, seen: Set<string>): { schema: Schema; ref?: string } => {
  const ref = typeof schema.$ref === 'string' ? schema.$ref : undefined
  if (!ref || seen.has(ref)) {
    return { schema, ref }
  }
  // Scalar's workspace store resolves references in loaded documents and keeps the target here.
  const resolved = schema['$ref-value']
  const target = isSchema(resolved) ? resolved : followRef(root, ref)
  return { schema: target ? { ...target, ...schema } : schema, ref }
}

/**
 * The type shown for a property. A `$ref` alone says nothing about the type, so it takes the type of
 * the schema it points to, or else that schema's name. An array shows its item type: `array<string>`.
 */
const describeType = (root: Schema, schema: Schema, ref: string | undefined, seen: Set<string>): string => {
  const own = typeName(schema.type)
  if (own === 'array' && isSchema(schema.items)) {
    const items = resolveSchema(root, schema.items, seen)
    return `array<${describeType(root, items.schema, items.ref, seen)}>`
  }
  if (own) {
    return own
  }
  if (ref) {
    return ref.split('/').pop() || ref
  }
  if (isSchema(schema.properties)) {
    return 'object'
  }
  return enumType(schema) ?? 'any'
}

/**
 * The type an `enum` or `const` implies when the schema states none, as .NET writes for an enum
 * serialized as text: `{ "enum": ["Succeeded", "Declined"] }` reads as `string`, not `any`.
 */
const enumType = (schema: Schema): string | undefined => {
  const values = Array.isArray(schema.enum) ? schema.enum : 'const' in schema ? [schema.const] : []
  const types = new Set(values.map((value) => (value === null ? 'null' : Array.isArray(value) ? 'array' : typeof value)))
  types.delete('undefined')
  const named = [...types].map((type) => (type === 'bigint' ? 'integer' : type))
  return named.length ? named.join(' | ') : undefined
}

const describeDetails = (schema: Schema): string[] | undefined => {
  const details: string[] = []
  if (typeof schema.format === 'string') {
    details.push(schema.format)
  }
  if (Array.isArray(schema.enum) && schema.enum.length) {
    details.push(`one of: ${schema.enum.map((value) => JSON.stringify(value)).join(', ')}`)
  }
  if (schema.const !== undefined) {
    details.push(`const: ${JSON.stringify(schema.const)}`)
  }
  if (schema.default !== undefined) {
    details.push(`default: ${JSON.stringify(schema.default)}`)
  }
  for (const [keyword, label] of [
    ['minimum', 'min'],
    ['maximum', 'max'],
    ['minLength', 'min length'],
    ['maxLength', 'max length'],
    ['minItems', 'min items'],
    ['maxItems', 'max items'],
  ] as const) {
    if (typeof schema[keyword] === 'number') {
      details.push(`${label}: ${schema[keyword]}`)
    }
  }
  if (typeof schema.pattern === 'string') {
    details.push(`pattern: ${schema.pattern}`)
  }
  return details.length ? details : undefined
}

const propertyRows = (root: Schema, schema: Schema, depth: number, seen: Set<string>): SchemaPropertyRow[] | undefined => {
  if (!isSchema(schema.properties) || depth > MAX_DEPTH) {
    return undefined
  }
  const requiredNames = new Set(Array.isArray(schema.required) ? schema.required : [])
  return Object.entries(schema.properties).map(([name, value]) => {
    const property = isSchema(value) ? value : {}
    const { schema: resolved, ref } = resolveSchema(root, property, seen)
    // A reference already being expanded higher up is shown by name only, so a recursive schema ends.
    const nested = ref ? new Set(seen).add(ref) : seen
    const container = typeName(resolved.type) === 'array' && isSchema(resolved.items)
      ? resolveSchema(root, resolved.items, nested)
      : { schema: resolved }
    const children =
      ref && seen.has(ref)
        ? undefined
        : propertyRows(root, container.schema, depth + 1, container.ref ? new Set(nested).add(container.ref) : nested)
    return {
      name,
      type: describeType(root, resolved, ref, seen),
      required: requiredNames.has(name),
      description: typeof resolved.description === 'string' ? resolved.description : '',
      details: describeDetails(resolved),
      children,
    }
  })
}

/**
 * Turns an object JSON Schema (the shape `workflow.inputs` takes in practice) into property rows,
 * nesting the properties of object and array-of-object properties. Returns `undefined` for any other
 * schema, which the caller shows as JSON.
 *
 * `$ref`s resolve against `root`: the schema itself by default (`#/$defs/…`), or the document it
 * comes from, such as an AsyncAPI payload pointing at `#/components/schemas/…`. The schema may
 * itself be such a reference.
 */
export const schemaPropertyRows = (schema: unknown, root?: unknown): SchemaPropertyRow[] | undefined => {
  if (!isSchema(schema)) {
    return undefined
  }
  const base = isSchema(root) ? root : schema
  const { schema: resolved, ref } = resolveSchema(base, schema, new Set())
  return propertyRows(base, resolved, 0, ref ? new Set([ref]) : new Set())
}

/**
 * The expression language a criterion is written in, for a badge next to its condition: `regex`,
 * `jsonpath` or `xpath`, with the version when the Criterion Expression Type Object names one.
 * `undefined` for a simple condition (the default, whether or not `type: simple` is written out),
 * since every condition without a badge reads as one.
 */
export const criterionTypeLabel = (type: ArazzoCriterion['type']): string | undefined => {
  const name = typeof type === 'string' ? type : type?.type
  if (!name || name === 'simple') {
    return undefined
  }
  const version = typeof type === 'object' && type.version ? ` ${type.version}` : ''
  return `${name}${version}`
}

/** `500 ms`, `30 s`, `2 min 30 s`: a Step Object `timeout` (milliseconds) in the largest units that fit. */
export const formatDuration = (milliseconds: number): string => {
  if (!Number.isFinite(milliseconds) || milliseconds < 1000) {
    return `${milliseconds} ms`
  }
  const seconds = milliseconds / 1000
  if (seconds < 60) {
    return `${Number.isInteger(seconds) ? seconds : seconds.toFixed(1)} s`
  }
  const minutes = Math.floor(seconds / 60)
  const rest = Math.round(seconds - minutes * 60)
  return rest ? `${minutes} min ${rest} s` : `${minutes} min`
}

/**
 * A Selector Object (`{ context, selector, type }`, Arazzo 1.1), or `undefined` for any other value.
 * Recognized by shape, since a selector can stand wherever a constant or Runtime Expression can.
 */
export const asSelector = (value: unknown): ArazzoSelector | undefined => {
  if (!value || typeof value !== 'object' || Array.isArray(value)) {
    return undefined
  }
  const candidate = value as Partial<ArazzoSelector>
  const typed = typeof candidate.type === 'string' || (!!candidate.type && typeof candidate.type === 'object')
  return typeof candidate.context === 'string' && typeof candidate.selector === 'string' && typed
    ? (candidate as ArazzoSelector)
    : undefined
}
