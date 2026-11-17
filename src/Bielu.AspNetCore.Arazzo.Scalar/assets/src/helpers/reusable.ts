import type { ArazzoDocument, ArazzoReusableObject } from '../types'

/** Matches a Reusable Object's `reference`: `$components.<kind>.<name>`. */
const COMPONENT_REFERENCE = /^\$components\.([^.]+)\.(.+)$/

const isReusable = (value: unknown): value is ArazzoReusableObject =>
  !!value && typeof value === 'object' && typeof (value as { reference?: unknown }).reference === 'string'

/**
 * Resolves an entry that may be a Reusable Object (`{ reference, value }`) into the component it
 * points at. Arazzo lets a referenced parameter override the component's `value`, so a `value` on
 * the reference wins. Returns `undefined` for a reference that does not resolve, so a dangling
 * reference is skipped rather than rendered as an empty row.
 */
export const resolveReusable = <T extends object>(
  document: ArazzoDocument,
  entry: T | ArazzoReusableObject | undefined,
): T | undefined => {
  if (!isReusable(entry)) {
    return entry as T | undefined
  }
  const match = COMPONENT_REFERENCE.exec(entry.reference)
  const component = match ? document.components?.[match[1]!]?.[match[2]!] : undefined
  if (!component || typeof component !== 'object') {
    return undefined
  }
  return (entry.value === undefined ? component : { ...component, value: entry.value }) as T
}

/** Resolves every entry of a list that may contain Reusable Objects, dropping the dangling ones. */
export const resolveReusables = <T extends object>(
  document: ArazzoDocument,
  entries: (T | ArazzoReusableObject)[] | undefined,
): T[] => (entries ?? []).flatMap((entry) => resolveReusable<T>(document, entry) ?? [])
