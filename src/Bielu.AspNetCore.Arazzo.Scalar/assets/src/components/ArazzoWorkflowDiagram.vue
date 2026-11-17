<script setup lang="ts">
import { computed, ref, useId } from 'vue'
import { INITIAL_PAN_ZOOM, panBy, panZoomForKey, type PanZoomState, zoomAt } from '../helpers/pan-zoom'
import { specLabel } from '../helpers/target-spec'
import { type LaidOutEdge, type LaidOutNode, SPEC_MARKER_WIDTH, type WorkflowLayout } from '../helpers/workflow-graph'

/**
 * Draws a laid-out workflow graph as SVG. Colors come from Scalar's theme variables, so the diagram
 * follows light and dark mode without re-rendering. Drag to pan, wheel to zoom, double-click to
 * reset; clicking a step scrolls to it.
 */
const props = defineProps<{
  layout: WorkflowLayout
  /** stepId → the id of that step's section, for click-to-scroll. */
  stepSectionIds: Record<string, string>
  /** Grow the diagram to fill its box (up to twice its natural size), as the expanded view does. */
  fit?: boolean
}>()

/** In `fit` mode the SVG fills the box and its viewBox keeps the proportions; the caps stop a tiny graph from ballooning. */
const fitStyle = computed(() =>
  props.fit
    ? { width: '100%', height: '100%', maxWidth: `${props.layout.width * 2}px`, maxHeight: `${props.layout.height * 2}px` }
    : undefined,
)

const markerPrefix = `ba-arrow-${useId()}`

const viewport = ref<HTMLElement>()
const panZoom = ref<PanZoomState>({ ...INITIAL_PAN_ZOOM })
const dragging = ref(false)
let dragPointerId: number | undefined
let lastPointer = { x: 0, y: 0 }
let moved = false

const resetView = () => {
  panZoom.value = { ...INITIAL_PAN_ZOOM }
}

defineExpose({ resetView })

const onWheel = (event: WheelEvent) => {
  event.preventDefault()
  const bounds = viewport.value?.getBoundingClientRect()
  const pivot = bounds ? { x: event.clientX - bounds.left, y: event.clientY - bounds.top } : { x: 0, y: 0 }
  panZoom.value = zoomAt(panZoom.value, event.deltaY < 0 ? 'in' : 'out', pivot)
}

const onPointerDown = (event: PointerEvent) => {
  if (event.button !== 0) {
    return
  }
  dragging.value = true
  moved = false
  dragPointerId = event.pointerId
  lastPointer = { x: event.clientX, y: event.clientY }
}

const onPointerMove = (event: PointerEvent) => {
  if (!dragging.value || event.pointerId !== dragPointerId) {
    return
  }
  const dx = event.clientX - lastPointer.x
  const dy = event.clientY - lastPointer.y
  if (!moved && Math.abs(dx) + Math.abs(dy) < 3) {
    return
  }
  if (!moved) {
    // Capture only once this is a drag, so a plain click still reaches the step it landed on.
    moved = true
    viewport.value?.setPointerCapture(event.pointerId)
  }
  lastPointer = { x: event.clientX, y: event.clientY }
  panZoom.value = panBy(panZoom.value, dx, dy)
}

const stopDragging = (event: PointerEvent) => {
  if (event.pointerId === dragPointerId) {
    dragging.value = false
    dragPointerId = undefined
  }
}

/** Arrow keys pan, `+`/`-` zoom around the middle, `0` resets; Enter on a step box opens that step. */
const onKeydown = (event: KeyboardEvent) => {
  if (event.altKey || event.ctrlKey || event.metaKey) {
    return
  }
  const bounds = viewport.value?.getBoundingClientRect()
  const center = bounds ? { x: bounds.width / 2, y: bounds.height / 2 } : { x: 0, y: 0 }
  const next = panZoomForKey(panZoom.value, event.key, center)
  if (next) {
    event.preventDefault()
    panZoom.value = next
  }
}

const onNodeKeydown = (event: KeyboardEvent, node: LaidOutNode) => {
  if (event.key === 'Enter' || event.key === ' ') {
    event.preventDefault()
    event.stopPropagation()
    moved = false
    openStep(node)
  }
}

const openStep = (node: LaidOutNode) => {
  const id = props.stepSectionIds[node.id]
  if (node.kind === 'step' && id && !moved) {
    document.getElementById(id)?.scrollIntoView({ behavior: 'smooth', block: 'start' })
  }
}

/** A path through dagre's bend points, with each corner rounded by a quadratic curve. */
const edgePath = (edge: LaidOutEdge): string => {
  const [first, ...rest] = edge.points
  if (!first) {
    return ''
  }
  if (rest.length < 2) {
    return `M${first.x},${first.y}${rest.map((point) => ` L${point.x},${point.y}`).join('')}`
  }
  let path = `M${first.x},${first.y}`
  for (let index = 0; index < rest.length - 1; index++) {
    const corner = rest[index]!
    const next = rest[index + 1]!
    const mid = { x: (corner.x + next.x) / 2, y: (corner.y + next.y) / 2 }
    path += ` Q${corner.x},${corner.y} ${mid.x},${mid.y}`
  }
  const last = rest[rest.length - 1]!
  return `${path} L${last.x},${last.y}`
}

const specName = (family: NonNullable<LaidOutNode['spec']>): string =>
  family === 'arazzo' ? 'Workflow' : specLabel({ family, version: undefined })

const viewBox = computed(() => `0 0 ${Math.max(props.layout.width, 1)} ${Math.max(props.layout.height, 1)}`)

const label = computed(() =>
  `Workflow diagram: ${props.layout.nodes
    .filter((node) => node.kind === 'step')
    .map((node) => node.label)
    .join(', ')}`,
)
</script>

<template>
  <div
    ref="viewport"
    aria-label="Workflow overview. Arrow keys pan, plus and minus zoom, 0 resets."
    class="ba-diagram"
    role="group"
    tabindex="0"
    :class="{ 'ba-diagram-dragging': dragging }"
    @dblclick="resetView"
    @keydown="onKeydown"
    @pointercancel="stopDragging"
    @pointerdown="onPointerDown"
    @pointermove="onPointerMove"
    @pointerup="stopDragging"
    @wheel="onWheel">
    <div
      class="ba-diagram-canvas"
      :class="{ 'ba-diagram-canvas-fit': fit }"
      :style="{ transform: `translate(${panZoom.x}px, ${panZoom.y}px) scale(${panZoom.scale})` }">
      <svg
        :aria-label="label"
        role="group"
        :style="fitStyle"
        :viewBox="viewBox"
        :width="layout.width"
        :height="layout.height">
        <defs>
          <marker
            v-for="kind in ['next', 'success', 'failure']"
            :id="`${markerPrefix}-${kind}`"
            :key="kind"
            markerHeight="8"
            markerWidth="8"
            orient="auto-start-reverse"
            refX="8"
            refY="4"
            viewBox="0 0 8 8">
            <path
              :class="`ba-diagram-arrow ba-diagram-${kind}`"
              d="M0,0 L8,4 L0,8 z" />
          </marker>
        </defs>

        <g
          v-for="(edge, index) in layout.edges"
          :key="`edge-${index}`">
          <path
            :class="`ba-diagram-edge ba-diagram-${edge.kind}`"
            :d="edgePath(edge)"
            :marker-end="`url(#${markerPrefix}-${edge.kind})`" />
          <g
            v-if="edge.label && edge.labelPosition"
            class="ba-diagram-edge-label">
            <rect
              :height="18"
              rx="3"
              :width="edge.labelWidth"
              :x="edge.labelPosition.x - edge.labelWidth / 2"
              :y="edge.labelPosition.y - 9" />
            <text
              dominant-baseline="central"
              text-anchor="middle"
              :x="edge.labelPosition.x"
              :y="edge.labelPosition.y">
              {{ edge.label }}
            </text>
          </g>
        </g>

        <g
          v-for="node in layout.nodes"
          :key="node.id"
          :aria-label="node.kind === 'step' && stepSectionIds[node.id] ? `Go to step ${node.label}${node.spec ? ` (${specName(node.spec)})` : ''}` : undefined"
          :class="[`ba-diagram-node ba-diagram-node-${node.kind}`, { 'ba-diagram-link': node.kind === 'step' && stepSectionIds[node.id] }]"
          :role="node.kind === 'step' && stepSectionIds[node.id] ? 'link' : undefined"
          :tabindex="node.kind === 'step' && stepSectionIds[node.id] ? 0 : undefined"
          @click="openStep(node)"
          @keydown="onNodeKeydown($event, node)">
          <title v-if="node.kind === 'step'">
            Go to step {{ node.label }}<template v-if="node.spec"> ({{ specName(node.spec) }})</template>
          </title>
          <rect
            v-if="node.kind === 'step'"
            :height="node.height"
            rx="6"
            :width="node.width"
            :x="node.x - node.width / 2"
            :y="node.y - node.height / 2" />
          <rect
            v-if="node.kind === 'step' && node.spec"
            :class="`ba-diagram-spec ba-spec-${node.spec}`"
            :height="node.height - 2"
            rx="5"
            :width="SPEC_MARKER_WIDTH"
            :x="node.x - node.width / 2 + 1"
            :y="node.y - node.height / 2 + 1" />
          <rect
            v-else-if="node.kind !== 'step'"
            :height="node.height"
            :rx="node.height / 2"
            :width="node.width"
            :x="node.x - node.width / 2"
            :y="node.y - node.height / 2" />
          <text
            dominant-baseline="central"
            text-anchor="middle"
            :x="node.spec ? node.x + SPEC_MARKER_WIDTH / 2 : node.x"
            :y="node.y">
            {{ node.label }}
          </text>
        </g>
      </svg>
    </div>
  </div>
</template>
