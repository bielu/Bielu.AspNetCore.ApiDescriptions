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

type Band = {
  stage: SequenceStage
  number: number
  top: number
  bottom: number
}

/**
 * Each message with its row (the first message of a step carries the step's name), and each stage with the
 * rows it spans: a title row, numbered in run order, then its workflow's messages.
 */
const layout = computed(() => {
  const { messages, stages } = props.sequence
  const bands: Band[] = []
  let y = HEADER + 24
  const rows = messages.map((message, index) => {
    const previous = messages[index - 1]
    const next = messages[index + 1]
    const stage = message.stage
      ? stages.find((candidate) => candidate.id === message.stage)
      : undefined
    if (stage && message.stage !== previous?.stage) {
      bands.push({ stage, number: bands.length + 1, top: y - 18, bottom: 0 })
      y += BAND
    }
    const row = {
      message,
      y,
      first: !sameStep(message, previous),
      x1: laneX.value.get(message.from) ?? GUTTER,
      x2: laneX.value.get(message.to) ?? GUTTER,
    }
    const bottom = y + (message.note ? NOTE : 0)
    y +=
      ROW +
      (message.note ? NOTE : 0) +
      (next && !sameStep(message, next) ? 8 : 0)
    if (stage && next?.stage !== message.stage) {
      bands[bands.length - 1]!.bottom = bottom + 12
      y += 8
    }
    return row
  })
  return { rows, bands }
})

const rows = computed(() => layout.value.rows)
const bands = computed(() => layout.value.bands)

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
  const band = bands.value[bands.value.length - 1]
  return Math.max(
    last ? last.y + (last.message.note ? NOTE : 0) + 24 : HEADER + 24,
    band ? band.bottom + 12 : 0,
  )
})

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
        :key="band.stage.id"
        class="ba-sequence-stage">
        <title>
          {{ band.number }} · {{ band.stage.label }} (workflow
          {{ band.stage.workflowId }}){{
            band.stage.note ? ` — ${band.stage.note}` : ''
          }}
        </title>
        <rect
          :height="band.bottom - band.top"
          rx="6"
          :width="width - 8"
          x="4"
          :y="band.top" />
        <g
          :aria-label="`Go to step ${band.stage.id}`"
          class="ba-sequence-stage-title"
          :class="{ 'ba-diagram-link': !!stepSectionIds[band.stage.id] }"
          role="link"
          tabindex="0"
          @click="open($event, band.stage.id, stepSectionIds[band.stage.id])"
          @keydown="
            onKeydown($event, band.stage.id, stepSectionIds[band.stage.id])
          ">
          <text
            dominant-baseline="central"
            x="12"
            :y="band.top + 13">
            {{ band.number }} · {{ cut(band.stage.label, 60) }}
          </text>
        </g>
        <text
          v-if="band.stage.note"
          class="ba-sequence-note"
          dominant-baseline="central"
          text-anchor="end"
          :x="width - 14"
          :y="band.top + 13">
          {{ cut(band.stage.note, 60) }}
        </text>
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
