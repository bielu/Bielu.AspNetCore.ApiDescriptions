/**
 * Edge geometry shared by the diagram plugins: a route's points in, an SVG path out. Nothing here places
 * anything; the caller's layout (dagre for Arazzo, LikeC4's own export for LikeC4) already did.
 */

/** A point in diagram coordinates. */
export type Point = { x: number; y: number }

const round = (value: number): number => Math.round(value * 10) / 10

const at = (point: Point): string => `${round(point.x)},${round(point.y)}`

/**
 * The SVG path of a route written the way Graphviz writes one, a cubic Bézier spline: the start point, then three
 * points per segment. A route that is not one is drawn through its points as straight lines.
 */
export const splinePath = (points: readonly Point[] | undefined): string => {
  if (!points || points.length < 2) {
    return ''
  }
  const [start, ...rest] = points
  if (rest.length % 3 !== 0) {
    return `M${at(start!)} ${rest.map((point) => `L${at(point)}`).join(' ')}`
  }
  const segments: string[] = []
  for (let index = 0; index < rest.length; index += 3) {
    segments.push(`C${at(rest[index]!)} ${at(rest[index + 1]!)} ${at(rest[index + 2]!)}`)
  }
  return `M${at(start!)} ${segments.join(' ')}`
}

/**
 * A path through a polyline's bend points (dagre's, for example), with each corner rounded by a quadratic curve
 * through the middle of the next segment. Two points or fewer are a straight line. Coordinates are written as given.
 */
export const roundedPolylinePath = (points: readonly Point[]): string => {
  const [first, ...rest] = points
  if (!first) {
    return ''
  }
  if (rest.length < 2) {
    return `M${first.x},${first.y}${rest.map((point) => ` L${point.x},${point.y}`).join('')}`
  }
  let path = `M${first.x},${first.y}`
  for (let index = 0; index < rest.length - 1; index++) {
    const corner = rest[index]!
    const next = rest[index + 1]!
    path += ` Q${corner.x},${corner.y} ${(corner.x + next.x) / 2},${(corner.y + next.y) / 2}`
  }
  const last = rest[rest.length - 1]!
  return `${path} L${last.x},${last.y}`
}

/** An arrowhead, drawn at the end of a line, and whether it is filled (`normal`) or outlined (`onormal`). */
export type ArrowHead = { path: string; filled: boolean }

/** How long an arrowhead is, along its line. */
export const ARROW_LENGTH = 10

/**
 * The arrowhead at `tip`, pointing away from `from` (the last control point before it). `head` is the arrow type
 * (LikeC4's names): `none` draws nothing, `onormal`, `odiamond` and `open` draw an outline, `diamond` a filled
 * diamond, `vee` an open V, `crow` a crow's foot, and anything else a filled triangle.
 */
export const arrowHead = (from: Point, tip: Point, head: string | undefined): ArrowHead | undefined => {
  if (head === 'none') {
    return undefined
  }
  let dx = tip.x - from.x
  let dy = tip.y - from.y
  const length = Math.hypot(dx, dy)
  if (length === 0) {
    dx = 1
    dy = 0
  } else {
    dx /= length
    dy /= length
  }
  const point = (along: number, across: number): string =>
    `${round(tip.x - dx * along - dy * across)},${round(tip.y - dy * along + dx * across)}`
  const half = ARROW_LENGTH * 0.45
  switch (head) {
    case 'diamond':
    case 'odiamond':
      return {
        path: `M${point(0, 0)} L${point(ARROW_LENGTH * 0.7, half)} L${point(ARROW_LENGTH * 1.4, 0)} L${point(ARROW_LENGTH * 0.7, -half)} Z`,
        filled: head === 'diamond',
      }
    case 'vee':
    case 'open':
      return { path: `M${point(ARROW_LENGTH, half)} L${point(0, 0)} L${point(ARROW_LENGTH, -half)}`, filled: false }
    case 'crow':
      // Three prongs from a point on the line, spreading out to the end it reaches.
      return {
        path: `M${point(0, half)} L${point(ARROW_LENGTH, 0)} L${point(0, -half)} M${point(ARROW_LENGTH, 0)} L${point(0, 0)}`,
        filled: false,
      }
    default:
      return { path: `M${point(0, 0)} L${point(ARROW_LENGTH, half)} L${point(ARROW_LENGTH, -half)} Z`, filled: head !== 'onormal' }
  }
}

/** The arrowhead at the target end of a route, and at its source end for a `tail`. */
export const edgeArrows = (points: readonly Point[] | undefined, head: string | undefined, tail: string | undefined): ArrowHead[] => {
  if (!points || points.length < 2) {
    return []
  }
  const arrows: (ArrowHead | undefined)[] = [arrowHead(points[points.length - 2]!, points[points.length - 1]!, head)]
  if (tail && tail !== 'none') {
    arrows.push(arrowHead(points[1]!, points[0]!, tail))
  }
  return arrows.filter((arrow): arrow is ArrowHead => arrow !== undefined)
}

/** The `stroke-dasharray` of a line style: `dashed`, `dotted`, or solid (`undefined`). */
export const dashArray = (line: string | undefined): string | undefined =>
  line === 'dashed' ? '6 5' : line === 'dotted' ? '1.5 4' : undefined
