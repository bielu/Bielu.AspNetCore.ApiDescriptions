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
    const type = property?.type
    return {
      name,
      type: Array.isArray(type) ? type.join(' | ') : typeof type === 'string' ? type : property?.$ref ? 'object' : 'any',
      required: requiredNames.has(name),
      description: typeof property?.description === 'string' ? property.description : '',
    }
  })
}
