import type { LikeC4Box, LikeC4Edge, LikeC4SequenceLayout, LikeC4View } from '../types'
import { stepNumber } from './view-layout'

/** How wide an activation bar (the box on a lifeline where a step starts or ends) is drawn. */
export const ACTIVATION_WIDTH = 10

/** How far right of its lifeline a self-step's loop reaches. */
const LOOP_WIDTH = 44

/** A lane: an actor's box at the top, and its lifeline down to the bottom of the view. */
export type SequenceLane = { id: string; box: LikeC4Box; lifeline: { x: number; top: number; bottom: number } }

/** A step, drawn from one lane's activation bar to another's (or back to its own). */
export type SequenceStep = {
  id: string
  number: string
  edge: LikeC4Edge
  /** The arrow's route. */
  points: [number, number][]
  /** Whether the step starts and ends on the same lane. */
  self: boolean
  /** Where the label goes: its anchor point, and how the text is anchored to it. */
  label: { x: number; y: number; anchor: 'middle' | 'start' }
}

/** What a sequence view is drawn from: the lanes, the activation bars, the steps, and any frames around them. */
export type SequenceDiagram = {
  bounds: LikeC4Box
  lanes: SequenceLane[]
  activations: LikeC4Box[]
  steps: SequenceStep[]
  /** Elements grouping lanes (an element shown with its children), and `parallel` blocks of steps. */
  frames: (LikeC4Box & { id: string; kind: 'compound' | 'parallel' })[]
}

const isBox = (value: unknown): value is LikeC4Box => {
  const box = value as Partial<LikeC4Box> | undefined
  return [box?.x, box?.y, box?.width, box?.height].every((part) => typeof part === 'number' && Number.isFinite(part))
}

/**
 * Turns LikeC4's sequence layout into what the view draws. LikeC4 places each lane and, on it, a port for each
 * step's end (`cx`/`cy`, relative to the lane's box); a step goes from its source port to its target port.
 * Steps whose ports are missing are left out, so a layout from another LikeC4 version draws what it can.
 */
export const sequenceDiagram = (view: LikeC4View): SequenceDiagram | undefined => {
  const layout: LikeC4SequenceLayout | undefined = view.sequenceLayout
  const actors = (layout?.actors ?? []).filter((actor) => isBox(actor) && typeof actor.id === 'string')
  if (!layout || actors.length === 0) {
    return undefined
  }
  const bounds = isBox(layout.bounds) ? layout.bounds : { x: 0, y: 0, width: 1, height: 1 }
  const bottom = bounds.y + bounds.height
  const ports = new Map<string, { x: number; y: number; height: number; lane: string }>()
  const activations: LikeC4Box[] = []
  for (const actor of actors) {
    for (const port of actor.ports ?? []) {
      const x = actor.x + port.cx
      const y = actor.y + port.cy
      const height = port.height ?? 24
      ports.set(port.id, { x, y, height, lane: actor.id })
      activations.push({ x: x - ACTIVATION_WIDTH / 2, y: y - height / 2, width: ACTIVATION_WIDTH, height })
    }
  }
  const lanes: SequenceLane[] = actors.map((actor) => ({
    id: actor.id,
    box: { x: actor.x, y: actor.y, width: actor.width, height: actor.height },
    lifeline: { x: actor.x + actor.width / 2, top: actor.y + actor.height, bottom },
  }))
  const edges = new Map((view.edges ?? []).map((edge) => [edge.id, edge]))
  const steps: SequenceStep[] = []
  ;(layout.steps ?? []).forEach((step, index) => {
    const source = ports.get(step.sourceHandle)
    const target = ports.get(step.targetHandle)
    const edge = edges.get(step.id)
    if (!source || !target || !edge) {
      return
    }
    const number = stepNumber(step.id) ?? String(index + 1)
    const half = ACTIVATION_WIDTH / 2
    if (source.lane === target.lane) {
      const out = source.x + half
      const loop = out + LOOP_WIDTH
      steps.push({
        id: step.id,
        number,
        edge,
        self: true,
        points: [
          [out, source.y],
          [loop, source.y],
          [loop, target.y],
          [out, target.y],
        ],
        label: { x: loop + 8, y: (source.y + target.y) / 2 + 4, anchor: 'start' },
      })
      return
    }
    const direction = target.x > source.x ? 1 : -1
    const from: [number, number] = [source.x + half * direction, source.y]
    const to: [number, number] = [target.x - half * direction, target.y]
    steps.push({
      id: step.id,
      number,
      edge,
      self: false,
      points: [from, to],
      label: { x: (from[0] + to[0]) / 2, y: Math.min(from[1], to[1]) - 8, anchor: 'middle' },
    })
  })
  const frames: SequenceDiagram['frames'] = [
    ...(layout.compounds ?? [])
      .filter((compound): compound is LikeC4Box & { id: string; origin?: string; depth?: number } => isBox(compound))
      .sort((a, b) => (a.depth ?? 0) - (b.depth ?? 0))
      .map((compound) => ({ ...compound, id: compound.origin ?? compound.id, kind: 'compound' as const })),
    ...(layout.parallelAreas ?? [])
      .filter((area): area is LikeC4Box & { parallelPrefix?: string } => isBox(area))
      .map((area, index) => ({ ...area, id: area.parallelPrefix ?? `parallel-${index}`, kind: 'parallel' as const })),
  ]
  return { bounds, lanes, activations, steps, frames }
}
