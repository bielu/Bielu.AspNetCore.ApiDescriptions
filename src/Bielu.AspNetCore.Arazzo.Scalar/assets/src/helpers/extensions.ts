/**
 * The Specification Extensions (`x-*` fields) of an Arazzo object, in document order. Scalar adds its
 * own `x-scalar-*` fields to loaded documents (such as the URL a document was loaded from); those are
 * Scalar's bookkeeping, not the author's, so they are left out.
 */
export const extensionsOf = (value: unknown): [string, unknown][] =>
  value && typeof value === 'object' && !Array.isArray(value)
    ? Object.entries(value).filter(([key]) => key.startsWith('x-') && !key.startsWith('x-scalar-'))
    : []

/**
 * Follows a top-level local `$ref` (such as `{ "$ref": "#/components/inputs/orderInput" }`) through
 * the Arazzo document, so a workflow's `inputs` can point at a reusable input schema. Scalar's
 * workspace store may already have resolved it (`$ref-value`); anything else is returned unchanged.
 */
export const resolveLocalRef = (document: unknown, value: unknown): unknown => {
  if (!value || typeof value !== 'object' || typeof (value as { $ref?: unknown }).$ref !== 'string') {
    return value
  }
  const resolved = (value as { '$ref-value'?: unknown })['$ref-value']
  if (resolved && typeof resolved === 'object') {
    return resolved
  }
  const ref = (value as { $ref: string }).$ref
  if (!ref.startsWith('#/')) {
    return value
  }
  const target = ref
    .slice(2)
    .split('/')
    .map((segment) => segment.replace(/~1/g, '/').replace(/~0/g, '~'))
    .reduce<unknown>(
      (node, segment) =>
        node && typeof node === 'object' && Object.prototype.hasOwnProperty.call(node, segment)
          ? (node as Record<string, unknown>)[segment]
          : undefined,
      document,
    )
  return target ?? value
}
