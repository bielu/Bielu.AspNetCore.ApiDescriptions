/**
 * The geometry the views are drawn with. LikeC4 has already laid every view out (boxes, edge routes, label
 * boxes), so nothing here places anything: it turns those coordinates into SVG paths. The shapes are LikeC4's; edge
 * paths, arrowheads and text wrapping are @bielu/scalar-graph's, with LikeC4's `[x, y]` points converted here.
 */

import { type ArrowHead, edgeArrows as graphEdgeArrows, type Point as GraphPoint, splinePath } from '@bielu/scalar-graph'

/** A point as LikeC4 writes it. */
type Point = [number, number]

/** The outline of a node's shape, in the node's own coordinates, plus the decoration lines some shapes have. */
export type ShapeGeometry = { body: string; details: string[] }

const round = (value: number): number => Math.round(value * 10) / 10

const roundedRect = (width: number, height: number, radius: number): string => {
  const r = Math.min(radius, width / 2, height / 2)
  return [
    `M${r},0`,
    `H${round(width - r)}`,
    `A${r},${r} 0 0 1 ${width},${r}`,
    `V${round(height - r)}`,
    `A${r},${r} 0 0 1 ${round(width - r)},${height}`,
    `H${r}`,
    `A${r},${r} 0 0 1 0,${round(height - r)}`,
    `V${r}`,
    `A${r},${r} 0 0 1 ${r},0`,
    'Z',
  ].join(' ')
}

/** How far each end of a queue (a cylinder on its side) reaches into it. */
export const queueCap = (width: number, height: number): number => round(Math.min(height * 0.18, width / 4))

/**
 * The outline of a LikeC4 shape, `width` × `height`. Shapes LikeC4 may add later fall back to a rectangle, so
 * a newer model still draws.
 */
export const shapeGeometry = (shape: string | undefined, width: number, height: number): ShapeGeometry => {
  const w = round(width)
  const h = round(height)
  switch (shape) {
    case 'person': {
      // A body with a head on top of it, the head overlapping the body's top edge.
      const head = round(Math.min(w, h) * 0.16)
      return {
        body: roundedRect(w, h, 12),
        details: [`M${round(w / 2 - head)},0 a${head},${head} 0 1 0 ${round(head * 2)},0 a${head},${head} 0 1 0 ${round(-head * 2)},0`],
      }
    }
    case 'queue': {
      // A cylinder on its side: an ellipse at each end.
      const rx = queueCap(w, h)
      return {
        body: `M${rx},0 H${round(w - rx)} A${rx},${round(h / 2)} 0 0 1 ${round(w - rx)},${h} H${rx} A${rx},${round(h / 2)} 0 0 1 ${rx},0 Z`,
        details: [`M${round(w - rx)},0 A${rx},${round(h / 2)} 0 0 0 ${round(w - rx)},${h}`],
      }
    }
    case 'cylinder':
    case 'storage': {
      // A cylinder standing up: an ellipse on top and at the bottom.
      const ry = round(Math.min(w * 0.08, h / 4))
      return {
        body: `M0,${ry} A${round(w / 2)},${ry} 0 0 1 ${w},${ry} V${round(h - ry)} A${round(w / 2)},${ry} 0 0 1 0,${round(h - ry)} Z`,
        details: [`M0,${ry} A${round(w / 2)},${ry} 0 0 0 ${w},${ry}`],
      }
    }
    case 'browser': {
      // A window with a title bar and its three buttons.
      const bar = 24
      const dots = [16, 30, 44].map((x) => `M${x - 4},12 a4,4 0 1 0 8,0 a4,4 0 1 0 -8,0`)
      return { body: roundedRect(w, h, 8), details: [`M0,${bar} H${w}`, ...dots] }
    }
    case 'mobile': {
      // A phone held sideways: a speaker on the left, a button on the right.
      return {
        body: roundedRect(w, h, 14),
        details: [`M14,${round(h / 2 - 12)} V${round(h / 2 + 12)}`, `M${round(w - 18)},${round(h / 2)} a4,4 0 1 0 8,0 a4,4 0 1 0 -8,0`],
      }
    }
    case 'component': {
      // UML's component symbol: two tabs on the left edge.
      const tab = (y: number) => `M-10,${y} h20 v12 h-20 Z`
      return { body: roundedRect(w, h, 4), details: [tab(round(h * 0.25 - 6)), tab(round(h * 0.6 - 6))] }
    }
    case 'bucket': {
      // A bucket: wider at the top than at the bottom.
      const inset = round(w * 0.08)
      return { body: `M0,0 H${w} L${round(w - inset)},${h} H${inset} Z`, details: [] }
    }
    case 'document': {
      // A page with a wavy bottom edge.
      const wave = round(Math.min(h * 0.08, 12))
      return {
        body: `M0,0 H${w} V${round(h - wave)} C${round(w * 0.75)},${round(h - wave * 3)} ${round(w * 0.25)},${round(h + wave)} 0,${round(h - wave)} Z`,
        details: [],
      }
    }
    default:
      return { body: roundedRect(w, h, 6), details: [] }
  }
}

/** LikeC4 writes points as `[x, y]` tuples; @bielu/scalar-graph takes `{ x, y }`. */
const toPoints = (points: readonly Point[] | undefined): GraphPoint[] | undefined => points?.map(([x, y]) => ({ x, y }))

/**
 * The SVG path of an edge's route. LikeC4 writes it as Graphviz does, a cubic Bézier spline: the start point,
 * then three points per segment. A route that is not one is drawn through its points as straight lines.
 */
export const edgePath = (points: readonly Point[] | undefined): string => splinePath(toPoints(points))

/** The arrowhead at the target end of an edge's route, and at its source end for a `tail` (LikeC4's arrow types). */
export const edgeArrows = (points: readonly Point[] | undefined, head: string | undefined, tail: string | undefined): ArrowHead[] =>
  graphEdgeArrows(toPoints(points), head, tail)

export { dashArray, wrapText } from '@bielu/scalar-graph'
