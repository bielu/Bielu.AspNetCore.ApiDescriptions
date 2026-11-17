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

/** One row of a workflow's `inputs` JSON Schema, flattened for display. */
export type SchemaPropertyRow = {
  name: string
  type: string
  required: boolean
  description: string
}

/**
 * Flattens the top-level properties of an object JSON Schema (the shape `workflow.inputs` takes in
 * practice) into rows. Returns `undefined` for any other schema, which the caller shows as JSON.
 */
export const schemaPropertyRows = (schema: unknown): SchemaPropertyRow[] | undefined => {
  if (!schema || typeof schema !== 'object') {
    return undefined
  }
  const { properties, required } = schema as { properties?: unknown; required?: unknown }
  if (!properties || typeof properties !== 'object') {
    return undefined
  }
  const requiredNames = new Set(Array.isArray(required) ? required : [])
  return Object.entries(properties as Record<string, Record<string, unknown> | undefined>).map(([name, property]) => {
    return {
      name,
      type: propertyType(schema, property),
      required: requiredNames.has(name),
      description: typeof property?.description === 'string' ? property.description : '',
    }
  })
}

const typeName = (type: unknown): string | undefined =>
  Array.isArray(type) ? type.join(' | ') : typeof type === 'string' ? type : undefined

/**
 * The type shown for one property. A `$ref` alone says nothing about the type, so the row takes the
 * type of the schema it points to when that lives in the same schema (`#/$defs/…`), and otherwise
 * shows the referenced schema's name.
 */
const propertyType = (root: object, property: Record<string, unknown> | undefined): string => {
  const own = typeName(property?.type)
  if (own) {
    return own
  }
  const ref = property?.$ref
  if (typeof ref !== 'string') {
    return 'any'
  }
  const target = (ref.startsWith('#') ? parseJsonPointer(ref.slice(1)) : []).reduce<unknown>(
    (node, segment) => (node && typeof node === 'object' ? (node as Record<string, unknown>)[segment] : undefined),
    ref.startsWith('#') ? root : undefined,
  )
  const referenced = target && typeof target === 'object' ? typeName((target as Record<string, unknown>).type) : undefined
  return referenced ?? ref.split('/').pop() ?? ref
}
