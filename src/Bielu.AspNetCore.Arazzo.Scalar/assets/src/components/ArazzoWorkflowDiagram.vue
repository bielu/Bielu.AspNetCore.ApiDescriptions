<script setup lang="ts">
import { computed, onBeforeUnmount, onMounted, ref, useId, watch } from 'vue'
import { STEP_EXPAND_EVENT } from '../helpers/navigation'
import { INITIAL_PAN_ZOOM, panBy, panZoomForKey, type PanZoomState, zoomAt } from '../helpers/pan-zoom'
import { specLabel } from '../helpers/target-spec'
import {
  type LaidOutEdge,
  type LaidOutNode,
  GROUP_LABEL_HEIGHT,
  GROUP_LABEL_INSET,
  retryBadge,
  retryBadgeWidth,
  SPEC_MARKER_WIDTH,
  WORKFLOW_TOGGLE_WIDTH,
  truncateLabel,
  type WorkflowLayout,
} from '../helpers/workflow-graph'

/**
 * Draws a laid-out workflow graph as SVG. Colors come from Scalar's theme variables, so the diagram
 * follows light and dark mode without re-rendering. Drag to pan, wheel to zoom, double-click to
 * reset; clicking a step scrolls to it. A workflow node's +/− button shows or hides that workflow's steps
 * (the parent keeps which are shown, so the inline and the expanded diagram agree).
 */
const props = defineProps<{
  layout: WorkflowLayout
  /** stepId → the id of that step's section, for click-to-scroll. */
  stepSectionIds: Record<string, string>
  /** Grow the diagram to fill its box (up to twice its natural size), as the expanded view does. */
  fit?: boolean
}>()

const emit = defineEmits<{
  /** A workflow node's +/− was used: show or hide the steps of the workflow it invokes. */
  toggleWorkflow: [nodeId: string]
  /** A step box was used to open its step, so a full-screen view can get out of the way. */
  openStep: [stepId: string]
}>()

/**
 * The scale the graph is drawn at. It is measured when the view is shown, reset, or its box changes width:
 * inline, the graph shrinks to the box's width (never grows); in `fit` mode it fills the box (at most twice its
 * natural size). It is not measured again when the graph changes, so expanding or collapsing a workflow never
 * zooms the view the reader has set.
 */
const baseScale = ref(1)
const svgStyle = computed(() => ({
  width: `${props.layout.width * baseScale.value}px`,
  height: `${props.layout.height * baseScale.value}px`,
}))

const measureScale = () => {
  const box = viewport.value
  if (!box || !props.layout.width || !props.layout.height) {
    return
  }
  const style = getComputedStyle(box)
  const width = box.clientWidth - parseFloat(style.paddingLeft || '0') - parseFloat(style.paddingRight || '0')
  const height = box.clientHeight - parseFloat(style.paddingTop || '0') - parseFloat(style.paddingBottom || '0')
  if (width <= 0) {
    return
  }
  baseScale.value = props.fit
    ? height > 0
      ? Math.min(width / props.layout.width, height / props.layout.height, 2)
      : baseScale.value
    : Math.min(width / props.layout.width, 1)
}

let observer: ResizeObserver | undefined
let observedWidth = 0
let observedHeight = 0
onMounted(() => {
  measureScale()
  if (typeof ResizeObserver !== 'undefined' && viewport.value) {
    observedWidth = viewport.value.clientWidth
    observedHeight = viewport.value.clientHeight
    observer = new ResizeObserver(() => {
      // Inline, only a change of the box's width (a resized window) rescales: the box growing taller around an
      // expanded workflow does not. In `fit` mode the box is the window, so its height counts too, or a
      // shorter window would clip a graph scaled to the old height.
      const width = viewport.value?.clientWidth ?? 0
      const height = viewport.value?.clientHeight ?? 0
      if (width !== observedWidth || (props.fit && height !== observedHeight)) {
        observedWidth = width
        observedHeight = height
        measureScale()
      }
    })
    observer.observe(viewport.value)
  }
})
onBeforeUnmount(() => observer?.disconnect())

const markerPrefix = `ba-arrow-${useId()}`

const viewport = ref<HTMLElement>()
const panZoom = ref<PanZoomState>({ ...INITIAL_PAN_ZOOM })
const dragging = ref(false)
let dragPointerId: number | undefined
let lastPointer = { x: 0, y: 0 }
let moved = false

const resetView = () => {
  panZoom.value = { ...INITIAL_PAN_ZOOM }
  measureScale()
}

/** The workflow node just toggled and where it was on screen, so the relaid-out graph can be moved back under it. */
let anchor: { id: string; left: number; top: number } | undefined

const nodeElement = (id: string): Element | null | undefined =>
  viewport.value?.querySelector(`[data-node-id="${id.replace(/["\\]/g, '\\$&')}"]`)

watch(
  () => props.layout,
  () => {
    const pending = anchor
    anchor = undefined
    const after = pending && nodeElement(pending.id)?.getBoundingClientRect()
    if (pending && after) {
      panZoom.value = panBy(panZoom.value, pending.left - after.left, pending.top - after.top)
    }
  },
  { flush: 'post' },
)

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

const toggleWorkflow = (node: LaidOutNode) => {
  if (node.expandable && !moved) {
    const before = nodeElement(node.id)?.getBoundingClientRect()
    anchor = before ? { id: node.id, left: before.left, top: before.top } : undefined
    emit('toggleWorkflow', node.id)
  }
}

const onToggleKeydown = (event: KeyboardEvent, node: LaidOutNode) => {
  if (event.key === 'Enter' || event.key === ' ') {
    event.preventDefault()
    event.stopPropagation()
    moved = false
    toggleWorkflow(node)
  }
}

/** Only top-level steps have a section on the page; nested ones are shown in their own workflow. */
const sectionOf = (node: LaidOutNode): string | undefined => (node.group ? undefined : props.stepSectionIds[node.id])

const isStepLink = (node: LaidOutNode): boolean => (node.kind === 'step' || node.kind === 'workflow') && Boolean(sectionOf(node))

/** A workflow an action invokes has no step to open: the box itself toggles it. */
const opensByToggling = (node: LaidOutNode): boolean => node.kind === 'workflow' && !isStepLink(node) && Boolean(node.expandable)

const onNodeClick = (node: LaidOutNode) => (opensByToggling(node) ? toggleWorkflow(node) : openStep(node))

/** A step's own title, then the retries of the step itself, one per line. */
const nodeTitle = (node: LaidOutNode): string | undefined => {
  const title = baseTitle(node)
  const retries = (node.retries ?? []).map((retry) => `Retries itself: ${retry.label}${retry.action ? ` (${retry.action})` : ''}`)
  return retries.length ? [title, ...retries].filter(Boolean).join('\n') : title
}

const baseTitle = (node: LaidOutNode): string | undefined => {
  if (node.kind === 'workflow') {
    const what = node.stepId ? `Step ${node.label} invokes workflow ${node.workflowId}` : `Workflow ${node.workflowId}`
    if (node.cycle) {
      return `${what} (already expanded above: not expanded again)`
    }
    return node.expandable ? `${what}. ${node.expanded ? 'Hide' : 'Show'} its steps with the button.` : what
  }
  if (node.kind === 'failure' && node.actions?.length) {
    return `Ends the workflow: ${node.actions.join(', ')}`
  }
  if (node.kind === 'step') {
    return `Go to step ${node.label}${node.spec ? ` (${specName(node.spec)})` : ''}`
  }
  return undefined
}

const openStep = (node: LaidOutNode) => {
  const id = sectionOf(node)
  if (isStepLink(node) && id && !moved) {
    emit('openStep', node.id)
    const section = document.getElementById(id)
    section?.dispatchEvent(new CustomEvent(STEP_EXPAND_EVENT))
    section?.scrollIntoView({ behavior: 'smooth', block: 'start' })
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

/** The toggle's width on the right of a workflow node; 0 for any other box. */
const toggleWidth = (node: LaidOutNode): number => (node.kind === 'workflow' && (node.expandable || node.cycle) ? WORKFLOW_TOGGLE_WIDTH : 0)

/** The label sits between the spec marker on the left and the retry badge and toggle on the right. */
const labelX = (node: LaidOutNode): number =>
  node.x + (node.spec ? SPEC_MARKER_WIDTH / 2 : 0) - (toggleWidth(node) + retryBadgeWidth(node)) / 2

/** The retry badge's box: right of the label, left of the toggle, vertically centered. */
const retryBadgeBox = (node: LaidOutNode) => {
  const width = retryBadgeWidth(node) - 6
  return { x: node.x + node.width / 2 - toggleWidth(node) - width - 4, y: node.y - 9, width, height: 18 }
}

const viewBox = computed(() => `0 0 ${Math.max(props.layout.width, 1)} ${Math.max(props.layout.height, 1)}`)

const label = computed(() =>
  `Workflow diagram: ${props.layout.nodes
    .filter((node) => node.kind === 'step' || node.kind === 'workflow')
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
    :class="{ 'ba-diagram-dragging': dragging, 'ba-diagram-grown': !fit && (layout.groups?.length ?? 0) > 0 }"
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
        :style="svgStyle"
        :viewBox="viewBox"
        :width="layout.width"
        :height="layout.height">
        <defs>
          <marker
            v-for="kind in ['next', 'success', 'failure', 'return', 'expand']"
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
          v-for="group in layout.groups ?? []"
          :key="group.id"
          class="ba-diagram-group">
          <rect
            :height="group.height"
            rx="8"
            :width="group.width"
            :x="group.x - group.width / 2"
            :y="group.y - group.height / 2" />
          <title v-if="truncateLabel(group.label) !== group.label">{{ group.label }}</title>
          <text
            dominant-baseline="central"
            :x="group.x - group.width / 2 + GROUP_LABEL_INSET"
            :y="group.y - group.height / 2 + GROUP_LABEL_HEIGHT / 2">
            {{ truncateLabel(group.label) }}
          </text>
        </g>

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
            <title v-if="edge.action">{{ edge.action }}</title>
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
          :aria-label="isStepLink(node) ? `Go to step ${node.label}${node.spec ? ` (${specName(node.spec)})` : ''}` : undefined"
          :class="[
            `ba-diagram-node ba-diagram-node-${node.kind}`,
            {
              'ba-diagram-link': isStepLink(node) || opensByToggling(node),
              'ba-diagram-node-expanded': node.expanded,
              'ba-diagram-node-cycle': node.cycle,
            },
          ]"
          :data-node-id="node.id"
          :role="isStepLink(node) ? 'link' : undefined"
          :tabindex="isStepLink(node) ? 0 : undefined"
          @click="onNodeClick(node)"
          @keydown="isStepLink(node) && onNodeKeydown($event, node)">
          <title v-if="nodeTitle(node)">{{ nodeTitle(node) }}</title>
          <rect
            v-if="node.kind === 'step' || node.kind === 'workflow'"
            :height="node.height"
            rx="6"
            :width="node.width"
            :x="node.x - node.width / 2"
            :y="node.y - node.height / 2" />
          <rect
            v-if="(node.kind === 'step' || node.kind === 'workflow') && node.spec"
            :class="`ba-diagram-spec ba-spec-${node.spec}`"
            :height="node.height - 2"
            rx="5"
            :width="SPEC_MARKER_WIDTH"
            :x="node.x - node.width / 2 + 1"
            :y="node.y - node.height / 2 + 1" />
          <rect
            v-if="node.kind === 'success' || node.kind === 'failure'"
            :height="node.height"
            :rx="node.height / 2"
            :width="node.width"
            :x="node.x - node.width / 2"
            :y="node.y - node.height / 2" />
          <text
            dominant-baseline="central"
            text-anchor="middle"
            :x="labelX(node)"
            :y="node.y">
            {{ node.label }}
          </text>
          <g
            v-if="retryBadge(node)"
            class="ba-diagram-retry">
            <rect
              :height="retryBadgeBox(node).height"
              rx="9"
              :width="retryBadgeBox(node).width"
              :x="retryBadgeBox(node).x"
              :y="retryBadgeBox(node).y" />
            <text
              dominant-baseline="central"
              text-anchor="middle"
              :x="retryBadgeBox(node).x + retryBadgeBox(node).width / 2"
              :y="node.y">
              {{ retryBadge(node) }}
            </text>
          </g>
          <g
            v-if="node.kind === 'workflow' && (node.expandable || node.cycle)"
            :aria-expanded="node.expandable ? node.expanded : undefined"
            :aria-label="node.cycle ? `Workflow ${node.workflowId} is already expanded above` : `${node.expanded ? 'Hide' : 'Show'} the steps of workflow ${node.workflowId}`"
            class="ba-diagram-toggle"
            :class="{ 'ba-diagram-toggle-cycle': node.cycle }"
            :role="node.expandable ? 'button' : 'img'"
            :tabindex="node.expandable ? 0 : undefined"
            @click.stop="toggleWorkflow(node)"
            @keydown="onToggleKeydown($event, node)">
            <rect
              :height="16"
              rx="4"
              :width="16"
              :x="node.x + node.width / 2 - WORKFLOW_TOGGLE_WIDTH + 1"
              :y="node.y - 8" />
            <text
              dominant-baseline="central"
              text-anchor="middle"
              :x="node.x + node.width / 2 - WORKFLOW_TOGGLE_WIDTH + 9"
              :y="node.y">
              {{ node.cycle ? '↻' : node.expanded ? '−' : '+' }}
            </text>
          </g>
        </g>
      </svg>
    </div>
  </div>
</template>
