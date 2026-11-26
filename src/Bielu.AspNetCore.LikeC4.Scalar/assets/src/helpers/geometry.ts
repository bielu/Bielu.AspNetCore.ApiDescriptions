/**
 * The geometry the views are drawn with. LikeC4 has already laid every view out (boxes, edge routes, label
 * boxes), so nothing here places anything: it turns those coordinates into SVG paths.
 */

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

/**
 * The SVG path of an edge's route. LikeC4 writes it as Graphviz does, a cubic Bézier spline: the start point,
 * then three points per segment. A route that is not one is drawn through its points as straight lines.
 */
export const edgePath = (points: readonly Point[] | undefined): string => {
  if (!points || points.length < 2) {
    return ''
  }
  const [start, ...rest] = points
  const at = (point: Point) => `${round(point[0])},${round(point[1])}`
  if (rest.length % 3 !== 0) {
    return `M${at(start!)} ${rest.map((point) => `L${at(point)}`).join(' ')}`
  }
  const segments: string[] = []
  for (let index = 0; index < rest.length; index += 3) {
    segments.push(`C${at(rest[index]!)} ${at(rest[index + 1]!)} ${at(rest[index + 2]!)}`)
  }
  return `M${at(start!)} ${segments.join(' ')}`
}

/** An arrowhead, drawn at the end of a line, and whether it is filled (`normal`) or outlined (`onormal`). */
export type ArrowHead = { path: string; filled: boolean }

/** How long an arrowhead is, along its line. */
export const ARROW_LENGTH = 10

/**
 * The arrowhead at `tip`, pointing away from `from` (the last control point before it). `head` is LikeC4's
 * arrow type: `none` draws nothing, `onormal`, `odiamond` and `open` draw an outline, `diamond` a filled
 * diamond, `vee` an open V, and anything else LikeC4's default filled triangle.
 */
export const arrowHead = (from: Point, tip: Point, head: string | undefined): ArrowHead | undefined => {
  if (head === 'none') {
    return undefined
  }
  let dx = tip[0] - from[0]
  let dy = tip[1] - from[1]
  const length = Math.hypot(dx, dy)
  if (length === 0) {
    dx = 1
    dy = 0
  } else {
    dx /= length
    dy /= length
  }
  const point = (along: number, across: number): string =>
    `${round(tip[0] - dx * along - dy * across)},${round(tip[1] - dy * along + dx * across)}`
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
    default:
      return { path: `M${point(0, 0)} L${point(ARROW_LENGTH, half)} L${point(ARROW_LENGTH, -half)} Z`, filled: head !== 'onormal' }
  }
}

/** The arrowhead at the target end of an edge's route, and at its source end for a `tail`. */
export const edgeArrows = (
  points: readonly Point[] | undefined,
  head: string | undefined,
  tail: string | undefined,
): ArrowHead[] => {
  if (!points || points.length < 2) {
    return []
  }
  const arrows: (ArrowHead | undefined)[] = [arrowHead(points[points.length - 2]!, points[points.length - 1]!, head)]
  if (tail && tail !== 'none') {
    arrows.push(arrowHead(points[1]!, points[0]!, tail))
  }
  return arrows.filter((arrow): arrow is ArrowHead => arrow !== undefined)
}

/** The `stroke-dasharray` of a LikeC4 line style. */
export const dashArray = (line: string | undefined): string | undefined =>
  line === 'dashed' ? '6 5' : line === 'dotted' ? '1.5 4' : undefined

/** About how wide a character of the diagram font is, as a share of its size. */
const CHARACTER_WIDTH = 0.56

/**
 * Wraps `text` into lines at most `width` wide at `fontSize`, breaking between words (a word longer than a
 * line is broken itself), and keeps at most `maxLines`, ending the last with an ellipsis when text is left over.
 * SVG text does not wrap, and the widths are estimated rather than measured, so the view draws the same
 * before it is on the page and in an exported file.
 */
export const wrapText = (text: string, width: number, fontSize: number, maxLines = Number.POSITIVE_INFINITY): string[] => {
  const perLine = Math.max(1, Math.floor(width / (fontSize * CHARACTER_WIDTH)))
  const words = text.split(/\s+/).filter(Boolean)
  const lines: string[] = []
  let current = ''
  for (const word of words) {
    let rest = word
    while (rest.length > perLine) {
      if (current) {
        lines.push(current)
        current = ''
      }
      lines.push(rest.slice(0, perLine))
      rest = rest.slice(perLine)
    }
    if (!current) {
      current = rest
    } else if (current.length + 1 + rest.length <= perLine) {
      current = `${current} ${rest}`
    } else {
      lines.push(current)
      current = rest
    }
  }
  if (current) {
    lines.push(current)
  }
  if (lines.length <= maxLines) {
    return lines
  }
  const kept = lines.slice(0, maxLines)
  const last = kept[maxLines - 1]!
  kept[maxLines - 1] = `${last.length >= perLine ? last.slice(0, Math.max(0, perLine - 1)) : last}…`
  return kept
}
