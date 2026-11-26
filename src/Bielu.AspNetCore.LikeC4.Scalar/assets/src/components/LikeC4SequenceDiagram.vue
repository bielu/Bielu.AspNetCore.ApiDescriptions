<script setup lang="ts">
import { computed } from 'vue'

import { lineColorOf } from '../helpers/colors'
import { arrowHead, dashArray, wrapText } from '../helpers/geometry'
import { plainText } from '../helpers/markdown'
import { type SequenceStep, sequenceDiagram } from '../helpers/sequence'
import { LABEL_FONTS } from '../helpers/view-layout'
import type { LikeC4CustomColor, LikeC4Node, LikeC4View } from '../types'
import LikeC4NodeShape from './LikeC4Node.vue'

/**
 * A dynamic view drawn as a sequence diagram (`variant sequence`), from the lanes and ports LikeC4 laid out:
 * each element's box at the top of its lifeline, an activation bar where a step starts or ends, and the steps
 * as numbered arrows between them, top to bottom.
 */
const props = defineProps<{
  view: LikeC4View
  customColors?: Record<string, LikeC4CustomColor>
  /** The titles of the model's views, by id, so an element or a step that opens one is a link to it. */
  viewTitles: Record<string, string>
}>()

const emit = defineEmits<{ navigate: [viewId: string] }>()

/** Room around the drawing, so outlines and arrowheads at its edge are not clipped. */
const MARGIN = 16

/** How wide a step's label may be before it wraps. */
const LABEL_WIDTH = 260

const diagram = computed(() => sequenceDiagram(props.view))

const nodes = computed(
  () => new Map((props.view.nodes ?? []).map((node) => [node.id, node])),
)

/** Each lane's element, placed on its lane's box (the view's nodes are laid out for the diagram variant). */
const lanes = computed(() =>
  (diagram.value?.lanes ?? []).flatMap((lane) => {
    const node = nodes.value.get(lane.id)
    return node
      ? [
          {
            ...lane,
            node: { ...node, ...lane.box, children: [] } as LikeC4Node,
          },
        ]
      : []
  }),
)

const opens = (viewId: string | null | undefined) =>
  viewId && viewId !== props.view.id ? props.viewTitles[viewId] : undefined

/** What each step is drawn with: its colour, arrowhead, label lines and tooltip. */
const steps = computed(() =>
  (diagram.value?.steps ?? []).map((step: SequenceStep) => {
    const color = lineColorOf(step.edge.color, props.customColors)
    const lines = wrapText(
      `${step.number}. ${step.edge.label ?? ''}`.trim(),
      LABEL_WIDTH,
      LABEL_FONTS.step.size,
      2,
    )
    const target = opens(step.edge.navigateTo)
    return {
      ...step,
      color,
      arrow: arrowHead(
        step.points[step.points.length - 2]!,
        step.points[step.points.length - 1]!,
        step.edge.head,
      ),
      // A label above its arrow grows upwards; a loop's grows downwards beside it.
      lines: lines.map((text, index) => ({
        text,
        y: step.self
          ? step.label.y + index * LABEL_FONTS.step.line
          : step.label.y - (lines.length - 1 - index) * LABEL_FONTS.step.line,
      })),
      opens: target,
      tooltip: [
        step.edge.label,
        plainText(step.edge.notes),
        target && `Opens ${target}`,
      ]
        .filter(Boolean)
        .join('\n'),
    }
  }),
)

const open = (viewId: string | null | undefined) => {
  if (viewId && opens(viewId)) {
    emit('navigate', viewId)
  }
}
</script>

<template>
  <svg
    v-if="diagram"
    class="bl-diagram bl-sequence"
    :data-view-id="view.id"
    :height="diagram.bounds.height + MARGIN * 2"
    role="img"
    :viewBox="`${diagram.bounds.x - MARGIN} ${diagram.bounds.y - MARGIN} ${diagram.bounds.width + MARGIN * 2} ${diagram.bounds.height + MARGIN * 2}`"
    :width="diagram.bounds.width + MARGIN * 2"
    xmlns="http://www.w3.org/2000/svg">
    <g
      v-for="frame in diagram.frames"
      :key="frame.id"
      class="bl-frame"
      :class="`bl-frame-${frame.kind}`">
      <rect
        :height="frame.height"
        rx="6"
        :width="frame.width"
        :x="frame.x"
        :y="frame.y" />
      <text
        :x="frame.x + 10"
        :y="frame.y + 18">
        {{
          frame.kind === 'parallel'
            ? 'par'
            : (nodes.get(frame.id)?.title ?? frame.id).toUpperCase()
        }}
      </text>
    </g>
    <line
      v-for="lane in diagram.lanes"
      :key="lane.id"
      class="bl-lifeline"
      :x1="lane.lifeline.x"
      :x2="lane.lifeline.x"
      :y1="lane.lifeline.top"
      :y2="lane.lifeline.bottom" />
    <rect
      v-for="(bar, index) in diagram.activations"
      :key="index"
      class="bl-activation"
      :height="bar.height"
      :width="bar.width"
      :x="bar.x"
      :y="bar.y" />
    <LikeC4NodeShape
      v-for="lane in lanes"
      :key="lane.id"
      :customColors="customColors"
      :node="lane.node"
      :opens="opens(lane.node.navigateTo)"
      @navigate="emit('navigate', $event)" />
    <g
      v-for="step in steps"
      :key="step.id"
      class="bl-step"
      :class="{ 'bl-link': step.opens }"
      :data-edge-id="step.id"
      :role="step.opens ? 'link' : undefined"
      :tabindex="step.opens ? 0 : undefined"
      @click="open(step.edge.navigateTo)"
      @keydown.enter.prevent="open(step.edge.navigateTo)"
      @keydown.space.prevent="open(step.edge.navigateTo)">
      <title>{{ step.tooltip }}</title>
      <polyline
        class="bl-edge-line"
        fill="none"
        :points="step.points.map((point) => point.join(',')).join(' ')"
        :stroke="step.color"
        :stroke-dasharray="dashArray(step.edge.line)" />
      <path
        v-if="step.arrow"
        class="bl-edge-arrow"
        :d="step.arrow.path"
        :fill="
          step.arrow.filled ? step.color : 'var(--scalar-background-1, #fff)'
        "
        :stroke="step.color" />
      <text
        v-for="(line, index) in step.lines"
        :key="index"
        class="bl-edge-title"
        :text-anchor="step.label.anchor"
        :x="step.label.x"
        :y="line.y">
        {{ line.text }}
      </text>
    </g>
  </svg>
</template>
