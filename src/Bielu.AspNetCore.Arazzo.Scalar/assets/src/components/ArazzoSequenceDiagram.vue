<script setup lang="ts">
import { computed, nextTick, useId } from 'vue'
import { goToAnchor } from '../helpers/navigation'
import {
  CLIENT_ID,
  type Sequence,
  type SequenceMessage,
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

/** Each message with its row: the first message of a step carries the step's name. */
const rows = computed(() => {
  let y = HEADER + 24
  return props.sequence.messages.map((message, index) => {
    const first =
      index === 0 ||
      props.sequence.messages[index - 1]!.stepId !== message.stepId
    const row = {
      message,
      y,
      first,
      x1: laneX.value.get(message.from) ?? GUTTER,
      x2: laneX.value.get(message.to) ?? GUTTER,
    }
    y +=
      ROW +
      (message.note ? NOTE : 0) +
      (index + 1 < props.sequence.messages.length &&
      props.sequence.messages[index + 1]!.stepId !== message.stepId
        ? 8
        : 0)
    return row
  })
})

const width = computed(() => GUTTER + props.sequence.participants.length * LANE)
const height = computed(() => {
  const last = rows.value[rows.value.length - 1]
  return last ? last.y + (last.message.note ? NOTE : 0) + 24 : HEADER + 24
})

/** A response or a delivered message is drawn dashed, as in UML: what comes back, rather than what is sent. */
const dashed = (message: SequenceMessage): boolean =>
  message.kind === 'response'

const open = (event: Event, stepId: string) => {
  const section = props.stepSectionIds[stepId]
  if (section) {
    const from = event.currentTarget as Element
    emit('navigate', stepId)
    void goToAnchor(from, { anchor: section, stepSection: section }, nextTick, {
      focus: true,
    })
  }
}

const onKeydown = (event: KeyboardEvent, stepId: string) => {
  if (event.key === 'Enter' || event.key === ' ') {
    event.preventDefault()
    open(event, stepId)
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

      <g
        v-for="participant in sequence.participants"
        :key="participant.id"
        class="ba-sequence-participant"
        :class="{ 'ba-sequence-client': isClient(participant.id) }">
        <title>
          {{
            [participant.label, participant.detail].filter(Boolean).join(' — ')
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
          :class="{ 'ba-diagram-link': !!stepSectionIds[row.message.stepId] }"
          role="link"
          tabindex="0"
          @click="open($event, row.message.stepId)"
          @keydown="onKeydown($event, row.message.stepId)">
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
        <line
          class="ba-sequence-line"
          :class="{ 'ba-sequence-dashed': dashed(row.message) }"
          :marker-end="`url(#${markerId})`"
          :x1="row.x1 + (row.x2 > row.x1 ? 4 : -4)"
          :x2="row.x2 + (row.x2 > row.x1 ? -4 : 4)"
          :y1="row.y"
          :y2="row.y" />
        <text
          class="ba-sequence-label"
          text-anchor="middle"
          :x="(row.x1 + row.x2) / 2"
          :y="row.y - 6">
          {{ cut(row.message.label, 36) }}
        </text>
        <text
          v-if="row.message.note"
          class="ba-sequence-note"
          text-anchor="middle"
          :x="(row.x1 + row.x2) / 2"
          :y="row.y + 13">
          {{ cut(row.message.note, 60) }}
        </text>
      </g>
    </svg>
  </div>
</template>
