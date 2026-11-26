import type { LikeC4CustomColor } from '../types'

/**
 * The hue of each colour LikeC4 names. The view draws an element as a tint of its hue on Scalar's
 * background, outlined in the hue itself, so a model reads the same in Scalar's light and dark themes and
 * `primary` follows the theme's accent.
 */
const HUES: Record<string, string> = {
  primary: 'var(--scalar-color-accent, #3b82f6)',
  secondary: '#0ea5e9',
  muted: '#64748b',
  blue: '#3b82f6',
  sky: '#0ea5e9',
  indigo: '#6366f1',
  slate: '#64748b',
  gray: '#737373',
  green: '#22c55e',
  amber: '#f59e0b',
  red: '#ef4444',
}

/** What an element or a relationship of a colour is drawn with. */
export type Palette = {
  /** The hue: an element's outline, a relationship's line. */
  stroke: string
  /** An element's fill: the hue tinted onto Scalar's background. */
  fill: string
}

/** A colour the specification defines itself (`specification { color brand #... }`) is used as it is. */
const customHue = (name: string, custom: Record<string, LikeC4CustomColor> | undefined): string | undefined => {
  const elements = custom?.[name]?.elements
  const hue = elements?.stroke ?? elements?.fill
  return typeof hue === 'string' && /^(#[0-9a-f]{3,8}|rgba?\([^)]*\)|hsla?\([^)]*\)|oklch\([^)]*\))$/i.test(hue) ? hue : undefined
}

/**
 * The palette of a colour name. `tint` is how much of the hue the fill takes, in percent: a container
 * (an element drawn with its children) takes its `style.opacity`, a plain element a light tint.
 */
export const paletteOf = (
  color: string | undefined,
  custom?: Record<string, LikeC4CustomColor>,
  tint = 16,
): Palette => {
  const name = color ?? 'primary'
  const stroke = customHue(name, custom) ?? HUES[name] ?? HUES.primary!
  const share = Math.min(100, Math.max(0, tint))
  return { stroke, fill: `color-mix(in srgb, ${stroke} ${share}%, var(--scalar-background-1, #fff))` }
}

/**
 * The line colour of a relationship. LikeC4 draws relationships `gray` by default, which reads best as
 * Scalar's own muted text colour.
 */
export const lineColorOf = (color: string | undefined, custom?: Record<string, LikeC4CustomColor>): string =>
  !color || color === 'gray' ? 'var(--scalar-color-3, #737373)' : paletteOf(color, custom).stroke
