<script setup lang="ts">
import { computed, nextTick, useId } from 'vue'
import { goToAnchor } from '../helpers/navigation'
import {
  CLIENT_ID,
  type Sequence,
  type SequenceMessage,
  type SequenceStage,
} from '../helpers/sequence'

/**
 * The workflow as a sequence diagram: a lane for the client and one per service or broker it talks to, and the
 * steps' requests, responses and messages top to bottom in the order they run. Each step's name on the left
 * opens the step, by pointer, Enter or Space alike, and emits `navigate` first so a full-window view can close.
 *
 * The diagram shrinks to its column only down to a readable scale; with more lanes than fit, it scrolls sideways.
 */
const props = defineProps<{
  sequence: Sequence
  /** stepId → the id of that step's section, so a step's name opens it. */
  stepSectionIds: Record<string, string>
  /** Grow to fill the full-window view, rather than fit the column beside the steps. */
  fit?: boolean
}>()

const emit = defineEmits<{
  /** A step's name was activated; the step is being opened. */
  navigate: [stepId: string]
}>()

const GUTTER = 120
const LANE = 210
const HEADER = 44
const ROW = 36
const NOTE = 16
/** A stage's title row, above its first message. */
const BAND = 26
/** A `par` fragment's tag row, above its first message. */
const PAR_HEAD = 20
/** How far a frame inside another is inset from it, on each side. */
const INSET = 8
/** How far a lane's call to itself loops out to the right. */
const SELF = 28
const MAX_LABEL = 28
/** The smallest the diagram is drawn at (11px labels at about 9px); narrower than that, it scrolls instead. */
const MIN_SCALE = 0.85

const markerId = `ba-seq-${useId()}`

const cut = (text: string, length = MAX_LABEL): string =>
  text.length > length ? `${text.slice(0, length - 1)}…` : text

const laneX = computed(
  () =>
    new Map(
      props.sequence.participants.map((participant, index) => [
        participant.id,
        GUTTER + index * LANE + LANE / 2,
      ]),
    ),
)

/** Whether two messages are of one step: the same id, in the same stage (a stage's step ids are its workflow's). */
const sameStep = (a: SequenceMessage, b: SequenceMessage | undefined) =>
  !!b && a.stepId === b.stepId && a.stage === b.stage

/**
 * A frame a message is drawn in: a stage's band, or a `par` or `alt` fragment; `branch` is the part it is in
 * (a `par`'s branch, an `alt`'s operand), and `guard` the operand's condition.
 */
type Frame = {
  key: string
  kind: 'stage' | 'par' | 'alt'
  branch: string
  stage?: SequenceStage
  guard?: string
}

/** A frame as laid out: the rows it spans, how deep it is nested, and where its branches are divided. */
type Fragment = {
  key: string
  kind: 'stage' | 'par' | 'alt'
  stage?: SequenceStage
  /** A stage's number, in run order. */
  number?: number
  /** An `alt`'s first operand's condition, beside its tag. */
  guard?: string
  top: number
  bottom: number
  depth: number
  /** The dashed lines between branches or operands, each with the condition of the operand under it. */
  separators: { y: number; guard?: string }[]
}

/**
 * The frames a message is in, outermost first: the `par` its top-level step is a branch of, the stage it is in,
 * the `par` its step is a branch of inside that stage, and the `alt` of the ways its step can end.
 */
const framesOf = (message: SequenceMessage): Frame[] => {
  const { stages, parallels } = props.sequence
  const own = message.stage ?? message.stepId
  const frames: Frame[] = []
  const outer = parallels.find(
    (parallel) => !parallel.scope && parallel.steps.includes(own),
  )
  if (outer) {
    frames.push({ key: `par:${outer.id}`, kind: 'par', branch: own })
  }
  const stage = message.stage
    ? stages.find((candidate) => candidate.id === message.stage)
    : undefined
  if (stage) {
    frames.push({
      key: `stage:${stage.id}`,
      kind: 'stage',
      branch: stage.id,
      stage,
    })
  }
  const inner = message.stage
    ? parallels.find(
        (parallel) =>
          parallel.scope === message.stage &&
          parallel.steps.includes(message.stepId),
      )
    : undefined
  if (inner) {
    frames.push({
      key: `par:${inner.id}`,
      kind: 'par',
      branch: message.stepId,
    })
  }
  // Innermost: the ways the message's step can end.
  if (message.operand) {
    frames.push({
      key: `alt:${message.operand.alt}`,
      kind: 'alt',
      branch: String(message.operand.index),
      guard: message.operand.guard,
    })
  }
  return frames
}

/**
 * Each message with its row (the first message of a step carries the step's name), and each frame with the rows
 * it spans: a stage has a title row, numbered in run order, and a `par` a row for its tag, with a dashed line
 * between its branches. Frames inside frames are inset, so every edge shows.
 */
const layout = computed(() => {
  const { messages } = props.sequence
  const fragments: Fragment[] = []
  const open: { fragment: Fragment; branch: string }[] = []
  let stageNumber = 0
  let y = HEADER + 24
  let lastBottom = y
  /** Closes the innermost open frames down to `depth`, each a little below the one inside it. */
  const close = (depth: number) => {
    let below = 12
    while (open.length > depth) {
      open.pop()!.fragment.bottom = lastBottom + below
      below += 8
      y += 8
    }
  }
  const rows = messages.map((message, index) => {
    const previous = messages[index - 1]
    const next = messages[index + 1]
    const frames = framesOf(message)
    let kept = 0
    while (
      kept < open.length &&
      kept < frames.length &&
      open[kept]!.fragment.key === frames[kept]!.key
    ) {
      kept++
    }
    close(kept)
    for (let depth = 0; depth < kept; depth++) {
      const entry = open[depth]!
      if (entry.branch !== frames[depth]!.branch) {
        // A new branch of a `par` or operand of an `alt`: a dashed line across it, with the operand's condition.
        entry.branch = frames[depth]!.branch
        const guard = frames[depth]!.guard
        y += guard ? 18 : 10
        entry.fragment.separators.push({
          y: y - (guard ? 39 : 31),
          ...(guard ? { guard } : {}),
        })
      }
    }
    for (let depth = kept; depth < frames.length; depth++) {
      const frame = frames[depth]!
      const fragment: Fragment = {
        key: frame.key,
        kind: frame.kind,
        ...(frame.stage ? { stage: frame.stage, number: ++stageNumber } : {}),
        ...(frame.guard ? { guard: frame.guard } : {}),
        top: y - 18,
        bottom: 0,
        depth,
        separators: [],
      }
      fragments.push(fragment)
      open.push({ fragment, branch: frame.branch })
      y += frame.kind === 'stage' ? BAND : PAR_HEAD
    }
    const row = {
      message,
      y,
      first: !sameStep(message, previous),
      x1: laneX.value.get(message.from) ?? GUTTER,
      x2: laneX.value.get(message.to) ?? GUTTER,
    }
    lastBottom = y + (message.note ? NOTE : 0)
    y +=
      ROW +
      (message.note ? NOTE : 0) +
      (next && !sameStep(message, next) ? 8 : 0)
    return row
  })
  close(0)
  return { rows, fragments }
})

const rows = computed(() => layout.value.rows)
/** Outermost first, so a frame inside another is drawn over it. */
const fragments = computed(() =>
  [...layout.value.fragments].sort((a, b) => a.depth - b.depth),
)
const bands = computed(() =>
  fragments.value.filter((fragment) => fragment.kind === 'stage'),
)
/** The `par` and `alt` fragments. */
const combined = computed(() =>
  fragments.value.filter((fragment) => fragment.kind !== 'stage'),
)

/** Roughly how wide a self-call's text is drawn: 11px code for the label, 10px sans for the note. */
const LABEL_CHAR = 6.7
const NOTE_CHAR = 5.6

/**
 * Wide enough for every lane, and for the text beside a lane's call to itself, which starts right of its loop
 * and so can run past the rightmost lane's edge.
 */
const width = computed(() =>
  Math.max(
    GUTTER + props.sequence.participants.length * LANE,
    ...rows.value
      .filter((row) => row.x1 === row.x2)
      .map(
        (row) =>
          row.x1 +
          SELF +
          10 +
          Math.max(
            cut(row.message.label, 36).length * LABEL_CHAR,
            row.message.note ? cut(row.message.note, 60).length * NOTE_CHAR : 0,
          ) +
          12,
      ),
  ),
)
const height = computed(() => {
  const last = rows.value[rows.value.length - 1]
  return Math.max(
    last ? last.y + (last.message.note ? NOTE : 0) + 24 : HEADER + 24,
    ...fragments.value.map((fragment) => fragment.bottom + 12),
  )
})

/** Where a frame's left edge is, inset by how deep it is nested. */
const inset = (fragment: Fragment) => 4 + fragment.depth * INSET

/**
 * The section a step's name opens. A stage's steps are listed inside the step that invokes their workflow, so
 * theirs is that step's section (their own ids can repeat this workflow's).
 */
const sectionOf = (message: SequenceMessage): string | undefined =>
  props.stepSectionIds[message.stage ?? message.stepId]

/** A response or a delivered message is drawn dashed, as in UML: what comes back, rather than what is sent. */
const dashed = (message: SequenceMessage): boolean =>
  message.kind === 'response'

const open = (event: Event, stepId: string, section: string | undefined) => {
  if (section) {
    const from = event.currentTarget as Element
    emit('navigate', stepId)
    void goToAnchor(from, { anchor: section, stepSection: section }, nextTick, {
      focus: true,
    })
  }
}

const onKeydown = (
  event: KeyboardEvent,
  stepId: string,
  section: string | undefined,
) => {
  if (event.key === 'Enter' || event.key === ' ') {
    event.preventDefault()
    open(event, stepId, section)
  }
}

const isClient = (id: string) => id === CLIENT_ID
</script>

<template>
  <div
    class="ba-sequence"
    :class="{ 'ba-sequence-fit': fit }">
    <svg
      :aria-label="`Sequence of ${sequence.messages.length} messages between ${sequence.participants.map((participant) => participant.label).join(', ')}`"
      role="group"
      :style="{
        minWidth: `${width * MIN_SCALE}px`,
        maxWidth: `${width * (fit ? 1.4 : 1)}px`,
      }"
      :viewBox="`0 0 ${width} ${height}`"
      width="100%">
      <defs>
        <marker
          :id="markerId"
          markerHeight="8"
          markerWidth="8"
          orient="auto-start-reverse"
          refX="8"
          refY="4"
          viewBox="0 0 8 8">
          <path
            class="ba-sequence-arrow"
            d="M0,0 L8,4 L0,8 z" />
        </marker>
      </defs>

      <!-- Behind the lifelines and messages: each stage's band, titled with its workflow. -->
      <g
        v-for="band in bands"
        :key="band.key"
        class="ba-sequence-stage">
        <title>
          {{ band.number }} · {{ band.stage!.label }} (workflow
          {{ band.stage!.workflowId }}){{
            band.stage!.note ? ` — ${band.stage!.note}` : ''
          }}
        </title>
        <rect
          :height="band.bottom - band.top"
          rx="6"
          :width="width - 2 * inset(band)"
          :x="inset(band)"
          :y="band.top" />
        <g
          :aria-label="`Go to step ${band.stage!.id}`"
          class="ba-sequence-stage-title"
          :class="{ 'ba-diagram-link': !!stepSectionIds[band.stage!.id] }"
          role="link"
          tabindex="0"
          @click="open($event, band.stage!.id, stepSectionIds[band.stage!.id])"
          @keydown="
            onKeydown($event, band.stage!.id, stepSectionIds[band.stage!.id])
          ">
          <text
            dominant-baseline="central"
            :x="inset(band) + 8"
            :y="band.top + 13">
            {{ band.number }} · {{ cut(band.stage!.label, 60) }}
          </text>
        </g>
        <text
          v-if="band.stage!.note"
          class="ba-sequence-note"
          dominant-baseline="central"
          text-anchor="end"
          :x="width - inset(band) - 10"
          :y="band.top + 13">
          {{ cut(band.stage!.note, 60) }}
        </text>
      </g>

      <!--
        UML combined fragments: a `par` frames steps that run at the same time, its branches divided by dashed
        lines; an `alt` frames the ways a step can end, each operand under its condition.
      -->
      <g
        v-for="fragment in combined"
        :key="fragment.key"
        class="ba-sequence-fragment"
        :class="`ba-sequence-${fragment.kind}`">
        <title>
          {{
            fragment.kind === 'par'
              ? 'Run at the same time'
              : 'The ways the step can end'
          }}
        </title>
        <rect
          :height="fragment.bottom - fragment.top"
          rx="4"
          :width="width - 2 * inset(fragment)"
          :x="inset(fragment)"
          :y="fragment.top" />
        <path
          class="ba-sequence-fragment-tag"
          :d="`M${inset(fragment)},${fragment.top} h30 v10 l-6,6 h-24 z`" />
        <text
          class="ba-sequence-fragment-label"
          dominant-baseline="central"
          :x="inset(fragment) + 5"
          :y="fragment.top + 8">
          {{ fragment.kind }}
        </text>
        <text
          v-if="fragment.guard"
          class="ba-sequence-guard"
          dominant-baseline="central"
          :x="inset(fragment) + 36"
          :y="fragment.top + 8">
          [{{ cut(fragment.guard, 60) }}]
          <title v-if="fragment.guard.length > 60">{{ fragment.guard }}</title>
        </text>
        <g
          v-for="separator in fragment.separators"
          :key="separator.y">
          <line
            class="ba-sequence-fragment-separator"
            :x1="inset(fragment)"
            :x2="width - inset(fragment)"
            :y1="separator.y"
            :y2="separator.y" />
          <text
            v-if="separator.guard"
            class="ba-sequence-guard"
            dominant-baseline="central"
            :x="inset(fragment) + 6"
            :y="separator.y + 9">
            [{{ cut(separator.guard, 60) }}]
            <title v-if="separator.guard.length > 60">
              {{ separator.guard }}
            </title>
          </text>
        </g>
      </g>

      <g
        v-for="participant in sequence.participants"
        :key="participant.id"
        class="ba-sequence-participant"
        :class="{ 'ba-sequence-client': isClient(participant.id) }">
        <title>
          {{
            [participant.label, participant.detail, participant.description]
              .filter(Boolean)
              .join(' — ')
          }}
        </title>
        <line
          class="ba-sequence-lifeline"
          :x1="laneX.get(participant.id)"
          :x2="laneX.get(participant.id)"
          :y1="HEADER"
          :y2="height - 8" />
        <rect
          :height="HEADER - 10"
          rx="6"
          :width="LANE - 24"
          :x="laneX.get(participant.id)! - (LANE - 24) / 2"
          y="4" />
        <rect
          v-if="participant.spec"
          :class="`ba-diagram-spec ba-spec-${participant.spec}`"
          :height="HEADER - 12"
          rx="5"
          width="6"
          :x="laneX.get(participant.id)! - (LANE - 24) / 2 + 1"
          y="5" />
        <text
          dominant-baseline="central"
          text-anchor="middle"
          :x="laneX.get(participant.id)"
          :y="participant.detail ? 14 : 4 + (HEADER - 10) / 2">
          {{ cut(participant.label, 24) }}
        </text>
        <text
          v-if="participant.detail"
          class="ba-sequence-detail"
          dominant-baseline="central"
          text-anchor="middle"
          :x="laneX.get(participant.id)"
          y="29">
          {{ cut(participant.detail, 30) }}
        </text>
      </g>

      <g
        v-for="(row, index) in rows"
        :key="index"
        class="ba-sequence-message"
        :class="`ba-sequence-${row.message.kind}`">
        <g
          v-if="row.first"
          :aria-label="`Go to step ${row.message.stepId}`"
          class="ba-sequence-step"
          :class="{ 'ba-diagram-link': !!sectionOf(row.message) }"
          role="link"
          tabindex="0"
          @click="open($event, row.message.stepId, sectionOf(row.message))"
          @keydown="
            onKeydown($event, row.message.stepId, sectionOf(row.message))
          ">
          <title>Go to step {{ row.message.stepId }}</title>
          <text
            dominant-baseline="central"
            x="8"
            :y="row.y">
            {{ cut(row.message.stepId, 16) }}
          </text>
        </g>
        <title>
          {{ row.message.label
          }}{{ row.message.note ? ` — ${row.message.note}` : '' }}
        </title>
        <!-- A lane calling itself (an actor that is also the step's target): a loop out to the right and back. -->
        <path
          v-if="row.x1 === row.x2"
          class="ba-sequence-line ba-sequence-self"
          :class="{ 'ba-sequence-dashed': dashed(row.message) }"
          :d="`M${row.x1 + 4},${row.y - 7} h${SELF} v14 h-${SELF - 4}`"
          fill="none"
          :marker-end="`url(#${markerId})`" />
        <line
          v-else
          class="ba-sequence-line"
          :class="{ 'ba-sequence-dashed': dashed(row.message) }"
          :marker-end="`url(#${markerId})`"
          :x1="row.x1 + (row.x2 > row.x1 ? 4 : -4)"
          :x2="row.x2 + (row.x2 > row.x1 ? -4 : 4)"
          :y1="row.y"
          :y2="row.y" />
        <text
          class="ba-sequence-label"
          :text-anchor="row.x1 === row.x2 ? 'start' : 'middle'"
          :x="row.x1 === row.x2 ? row.x1 + SELF + 10 : (row.x1 + row.x2) / 2"
          :y="row.x1 === row.x2 ? row.y + 4 : row.y - 6">
          {{ cut(row.message.label, 36) }}
        </text>
        <text
          v-if="row.message.note"
          class="ba-sequence-note"
          :text-anchor="row.x1 === row.x2 ? 'start' : 'middle'"
          :x="row.x1 === row.x2 ? row.x1 + SELF + 10 : (row.x1 + row.x2) / 2"
          :y="row.y + (row.x1 === row.x2 ? 17 : 13)">
          {{ cut(row.message.note, 60) }}
        </text>
      </g>
    </svg>
  </div>
</template>
