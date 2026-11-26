import type { LikeC4Box, LikeC4Edge, LikeC4Node, LikeC4View } from '../types'
import { plainText } from './markdown'
import { queueCap, wrapText } from './geometry'

/** One line of a label: its text, its baseline (relative to what it labels), and which part of the label it is. */
export type LabelLine = { text: string; y: number; part: 'title' | 'technology' | 'description' | 'step' }

/** The font size and line height of each part of a label. */
export const LABEL_FONTS = {
  title: { size: 17, line: 22 },
  technology: { size: 12, line: 16 },
  description: { size: 13, line: 18 },
  container: { size: 12, line: 16 },
  edge: { size: 13, line: 17 },
  // A sequence is drawn at the size of its 320-wide lanes, so its steps are labelled as large as LikeC4 does.
  step: { size: 16, line: 20 },
} as const

/** The space between a box's edge and its text. */
const PADDING = 16

/** Whether a node is drawn as a container: an element shown with its children inside it. */
export const isContainer = (node: LikeC4Node): boolean => (node.children?.length ?? 0) > 0

/**
 * The part of a node its label is centred in: the whole node, less a queue's ends, whose curves the text
 * would otherwise run into.
 */
export const labelArea = (node: LikeC4Node): { center: number; width: number } => {
  if (node.shape === 'queue' && !isContainer(node)) {
    const cap = queueCap(node.width, node.height)
    return { center: (node.width - cap) / 2, width: node.width - cap * 3 - PADDING }
  }
  return { center: node.width / 2, width: node.width - PADDING * 2 }
}

/**
 * The lines of a node's label, centred in the node: its title, its technology, and as much of its description
 * as fits. A container's label is only its title, at its top-left, so its children have the room.
 */
export const nodeLabel = (node: LikeC4Node): LabelLine[] => {
  const title = (node.title ?? node.id).trim()
  if (isContainer(node)) {
    const font = LABEL_FONTS.container
    return wrapText(title.toUpperCase(), node.width - PADDING * 2, font.size, 1).map((text) => ({
      text,
      y: (node.labelBBox?.y ?? 0) + font.size + 6,
      part: 'title',
    }))
  }
  const width = labelArea(node).width
  const titleLines = wrapText(title, width, LABEL_FONTS.title.size, 2)
  const technology = node.technology ? wrapText(`[${node.technology}]`, width, LABEL_FONTS.technology.size, 1) : []
  const used = titleLines.length * LABEL_FONTS.title.line + technology.length * LABEL_FONTS.technology.line
  const room = Math.floor((node.height - PADDING * 2 - used - 6) / LABEL_FONTS.description.line)
  const description =
    room > 0 ? wrapText(plainText(node.description), width, LABEL_FONTS.description.size, Math.min(room, 4)) : []
  const parts: { part: LabelLine['part']; lines: string[]; line: number; size: number; gap: number }[] = [
    { part: 'title' as const, lines: titleLines, ...LABEL_FONTS.title, gap: 0 },
    { part: 'technology' as const, lines: technology, ...LABEL_FONTS.technology, gap: 2 },
    { part: 'description' as const, lines: description, ...LABEL_FONTS.description, gap: 6 },
  ].filter((part) => part.lines.length > 0)
  const height = parts.reduce((total, part) => total + part.gap + part.lines.length * part.line, 0)
  // A person's head overlaps the top of its body, so its label sits a little lower.
  let y = (node.height - height) / 2 + (node.shape === 'person' ? 6 : 0)
  const lines: LabelLine[] = []
  for (const part of parts) {
    y += part.gap
    for (const text of part.lines) {
      // The baseline: a line's text sits about a fifth of its height above the bottom of its line box.
      lines.push({ text, y: Math.round(y + part.line * 0.78), part: part.part })
      y += part.line
    }
  }
  return lines
}

/** The number of a dynamic view's step, from its edge id (`step-01` is 1, `step-02.01` is 2.1). */
export const stepNumber = (edgeId: string): string | undefined => {
  const match = /^step-(\d+(?:\.\d+)*)$/.exec(edgeId)
  return match ? match[1]!.split('.').map((part) => String(Number(part))).join('.') : undefined
}

/** An edge's label lines, in the view's coordinates, inside its label box; numbered for a dynamic view's step. */
export const edgeLabel = (edge: LikeC4Edge, numbered: boolean): { box: LikeC4Box; lines: LabelLine[] } | undefined => {
  const box = edge.labelBBox
  const number = numbered ? stepNumber(edge.id) : undefined
  const label = [number ? `${number}.` : '', edge.label ?? ''].join(' ').trim()
  if (!box || (!label && !edge.technology)) {
    return undefined
  }
  const font = LABEL_FONTS.edge
  // LikeC4 sizes the box for its own font: allow a little more, so a word estimated wider still fits.
  const width = box.width + 24
  const lines: LabelLine[] = []
  let y = box.y
  for (const text of wrapText(label, width, font.size, 3)) {
    lines.push({ text, y: Math.round(y + font.line * 0.78), part: 'title' })
    y += font.line
  }
  if (edge.technology) {
    for (const text of wrapText(`[${edge.technology}]`, width, LABEL_FONTS.technology.size, 1)) {
      lines.push({ text, y: Math.round(y + LABEL_FONTS.technology.line * 0.78), part: 'technology' })
      y += LABEL_FONTS.technology.line
    }
  }
  return { box: { ...box, height: Math.max(box.height, y - box.y) }, lines }
}

/**
 * The nodes of a view in the order they are drawn: containers first, outermost first, so the elements inside
 * them are drawn over them.
 */
export const drawOrder = (nodes: readonly LikeC4Node[]): LikeC4Node[] =>
  [...nodes].sort((a, b) => {
    const containerA = isContainer(a) ? 0 : 1
    const containerB = isContainer(b) ? 0 : 1
    return containerA - containerB || (a.level ?? 0) - (b.level ?? 0)
  })

/** The area a view covers: LikeC4's `bounds`, else what its nodes cover. */
export const viewBounds = (view: LikeC4View): LikeC4Box => {
  const bounds = view.sequenceLayout?.bounds ?? view.bounds
  if (bounds && bounds.width > 0 && bounds.height > 0) {
    return bounds
  }
  const nodes = view.nodes ?? []
  if (nodes.length === 0) {
    return { x: 0, y: 0, width: 1, height: 1 }
  }
  const x = Math.min(...nodes.map((node) => node.x))
  const y = Math.min(...nodes.map((node) => node.y))
  return {
    x,
    y,
    width: Math.max(...nodes.map((node) => node.x + node.width)) - x,
    height: Math.max(...nodes.map((node) => node.y + node.height)) - y,
  }
}

/** Whether a view is drawn as a sequence diagram: a dynamic view LikeC4 laid out as one. */
export const isSequence = (view: LikeC4View): boolean =>
  view._type === 'dynamic' && view.variant === 'sequence' && (view.sequenceLayout?.actors?.length ?? 0) > 0

/** The steps of a dynamic view in order, each with its number. */
export const dynamicSteps = (view: LikeC4View): { edge: LikeC4Edge; number: string }[] =>
  view._type === 'dynamic'
    ? (view.edges ?? []).map((edge, index) => ({ edge, number: stepNumber(edge.id) ?? String(index + 1) }))
    : []
