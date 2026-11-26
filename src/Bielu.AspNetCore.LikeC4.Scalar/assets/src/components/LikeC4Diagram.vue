<script setup lang="ts">
import { computed } from 'vue'

import { drawOrder, isContainer, viewBounds } from '../helpers/view-layout'
import type { LikeC4CustomColor, LikeC4View } from '../types'
import LikeC4Edge from './LikeC4Edge.vue'
import LikeC4EdgeLabel from './LikeC4EdgeLabel.vue'
import LikeC4Node from './LikeC4Node.vue'

/**
 * A view drawn as a diagram, at the places LikeC4 laid it out at: containers, then the edges, then the
 * elements over them, and the edge labels on top. A dynamic view's edges are its steps, numbered.
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

const bounds = computed(() => viewBounds(props.view))
const nodes = computed(() => drawOrder(props.view.nodes ?? []))
const containers = computed(() => nodes.value.filter(isContainer))
const elements = computed(() =>
  nodes.value.filter((node) => !isContainer(node)),
)
const edges = computed(() => props.view.edges ?? [])
const numbered = computed(() => props.view._type === 'dynamic')

/** The title of the view `viewId` names, when it is another view of the model. */
const opens = (viewId: string | null | undefined) =>
  viewId && viewId !== props.view.id ? props.viewTitles[viewId] : undefined
</script>

<template>
  <svg
    class="bl-diagram"
    :data-view-id="view.id"
    :height="bounds.height + MARGIN * 2"
    role="img"
    :viewBox="`${bounds.x - MARGIN} ${bounds.y - MARGIN} ${bounds.width + MARGIN * 2} ${bounds.height + MARGIN * 2}`"
    :width="bounds.width + MARGIN * 2"
    xmlns="http://www.w3.org/2000/svg">
    <LikeC4Node
      v-for="node in containers"
      :key="node.id"
      :customColors="customColors"
      :node="node"
      :opens="opens(node.navigateTo)"
      @navigate="emit('navigate', $event)" />
    <LikeC4Edge
      v-for="edge in edges"
      :key="edge.id"
      :customColors="customColors"
      :edge="edge" />
    <LikeC4Node
      v-for="node in elements"
      :key="node.id"
      :customColors="customColors"
      :node="node"
      :opens="opens(node.navigateTo)"
      @navigate="emit('navigate', $event)" />
    <LikeC4EdgeLabel
      v-for="edge in edges"
      :key="edge.id"
      :edge="edge"
      :numbered="numbered"
      :opens="opens(edge.navigateTo)"
      @navigate="emit('navigate', $event)" />
  </svg>
</template>
