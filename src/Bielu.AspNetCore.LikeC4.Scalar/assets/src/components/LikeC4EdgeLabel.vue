<script setup lang="ts">
import { computed } from 'vue'

import { edgeLabel } from '../helpers/view-layout'
import type { LikeC4Edge } from '../types'

/** An edge's label, in the box LikeC4 placed it in, on a backdrop so a line passing under it stays readable. */
const props = defineProps<{
  edge: LikeC4Edge
  /** Whether the edge is a dynamic view's step, whose label starts with its number. */
  numbered?: boolean
  /** The title of the view the step opens, when that view is in the model. */
  opens?: string
}>()

const emit = defineEmits<{ navigate: [viewId: string] }>()

const label = computed(() => edgeLabel(props.edge, props.numbered ?? false))

const open = () => {
  if (props.opens && props.edge.navigateTo) {
    emit('navigate', props.edge.navigateTo)
  }
}
</script>

<template>
  <g
    v-if="label"
    class="bl-edge-label"
    :class="{ 'bl-link': opens }"
    :data-edge-id="edge.id"
    :role="opens ? 'link' : undefined"
    :tabindex="opens ? 0 : undefined"
    @click="open"
    @keydown.enter.prevent="open"
    @keydown.space.prevent="open">
    <title v-if="opens">Opens {{ opens }}</title>
    <rect
      class="bl-edge-label-backdrop"
      :height="label.box.height + 4"
      rx="4"
      :width="label.box.width + 8"
      :x="label.box.x - 4"
      :y="label.box.y - 2" />
    <text
      v-for="(line, index) in label.lines"
      :key="index"
      :class="`bl-edge-${line.part}`"
      :x="label.box.x + label.box.width / 2"
      text-anchor="middle"
      :y="line.y">
      {{ line.text }}
    </text>
  </g>
</template>
