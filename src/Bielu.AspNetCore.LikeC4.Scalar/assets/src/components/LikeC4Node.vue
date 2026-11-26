<script setup lang="ts">
import { computed } from 'vue'

import { paletteOf } from '../helpers/colors'
import { shapeGeometry } from '../helpers/geometry'
import { plainText } from '../helpers/markdown'
import { isContainer, labelArea, nodeLabel } from '../helpers/view-layout'
import type { LikeC4CustomColor, LikeC4Node } from '../types'

/**
 * One element of a view, drawn where LikeC4 placed it: its shape, tinted in its colour, and its label. An
 * element that opens another view is a link to it.
 */
const props = defineProps<{
  node: LikeC4Node
  customColors?: Record<string, LikeC4CustomColor>
  /** The title of the view the element opens, when that view is in the model. */
  opens?: string
}>()

const emit = defineEmits<{ navigate: [viewId: string] }>()

const container = computed(() => isContainer(props.node))

const shape = computed(() =>
  shapeGeometry(
    container.value ? 'rectangle' : props.node.shape,
    props.node.width,
    props.node.height,
  ),
)

const palette = computed(() =>
  paletteOf(
    props.node.color,
    props.customColors,
    container.value ? (props.node.style?.opacity ?? 15) : 16,
  ),
)

const label = computed(() => nodeLabel(props.node))

/** The tooltip: the title, kind and technology, the description, and where a click leads. */
const tooltip = computed(() =>
  [
    [
      props.node.title ?? props.node.id,
      props.node.kind && `(${props.node.kind})`,
    ]
      .filter(Boolean)
      .join(' '),
    props.node.technology,
    plainText(props.node.description),
    props.opens && `Opens ${props.opens}`,
  ]
    .filter(Boolean)
    .join('\n'),
)

const open = () => {
  if (props.opens && props.node.navigateTo) {
    emit('navigate', props.node.navigateTo)
  }
}
</script>

<template>
  <g
    class="bl-node"
    :class="{ 'bl-container': container, 'bl-link': opens }"
    :data-node-id="node.id"
    :role="opens ? 'link' : undefined"
    :tabindex="opens ? 0 : undefined"
    :transform="`translate(${node.x},${node.y})`"
    @click="open"
    @keydown.enter.prevent="open"
    @keydown.space.prevent="open">
    <title>{{ tooltip }}</title>
    <path
      class="bl-node-body"
      :d="shape.body"
      :fill="palette.fill"
      :stroke="palette.stroke"
      :stroke-dasharray="
        container && node.style?.border !== 'solid' ? '6 4' : undefined
      " />
    <path
      v-for="(detail, index) in shape.details"
      :key="index"
      class="bl-node-detail"
      :d="detail"
      :fill="palette.fill"
      :stroke="palette.stroke" />
    <text
      v-for="(line, index) in label"
      :key="index"
      :class="`bl-node-${line.part}`"
      :text-anchor="container ? 'start' : 'middle'"
      :x="container ? 12 : labelArea(node).center"
      :y="line.y">
      {{ line.text }}
    </text>
  </g>
</template>
