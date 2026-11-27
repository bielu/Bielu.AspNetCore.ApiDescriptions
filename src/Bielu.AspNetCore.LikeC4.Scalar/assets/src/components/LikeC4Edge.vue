<script setup lang="ts">
import { computed } from 'vue'

import { lineColorOf } from '../helpers/colors'
import { dashArray, edgeArrows, edgePath } from '../helpers/geometry'
import { plainText } from '../helpers/markdown'
import type { LikeC4CustomColor, LikeC4Edge } from '../types'

/**
 * One edge of a view, drawn along the route LikeC4 gave it, with its arrowheads. Its label is drawn by the
 * diagram (`LikeC4EdgeLabel`), above every node, so no element covers it.
 */
const props = defineProps<{
  edge: LikeC4Edge
  customColors?: Record<string, LikeC4CustomColor>
}>()

const color = computed(() => lineColorOf(props.edge.color, props.customColors))
const path = computed(() => edgePath(props.edge.points))
const arrows = computed(() =>
  edgeArrows(props.edge.points, props.edge.head, props.edge.tail),
)
</script>

<template>
  <g
    v-if="path"
    class="bl-edge"
    :data-edge-id="edge.id">
    <title>
      {{ [edge.label, plainText(edge.description)].filter(Boolean).join('\n') }}
    </title>
    <path
      class="bl-edge-line"
      :d="path"
      fill="none"
      :stroke="color"
      :stroke-dasharray="dashArray(edge.line)" />
    <path
      v-for="(arrow, index) in arrows"
      :key="index"
      class="bl-edge-arrow"
      :d="arrow.path"
      :fill="arrow.filled ? color : 'var(--scalar-background-1, #fff)'"
      :stroke="color" />
  </g>
</template>
