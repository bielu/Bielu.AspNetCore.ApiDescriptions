<script lang="ts">
import type { WorkflowView } from '../helpers/workflow-graph'

/** The id of the tab for `view`, which labels the panel `panelId` names while that view is shown. */
export const viewTabId = (panelId: string, view: WorkflowView) =>
  `${panelId}-tab-${view}`
</script>

<script setup lang="ts">
/**
 * The overview's views as tabs on a row of their own, so every view's name stays on one line however narrow the
 * overview is; a strip too narrow for them all scrolls sideways.
 *
 * The tabs follow the WAI-ARIA tabs pattern: only the shown view's tab is in the tab order, the arrow keys, Home and
 * End move to another tab and show its view, and every tab controls the panel `panelId` names.
 */
const props = defineProps<{
  views: { value: WorkflowView; label: string }[]
  view: WorkflowView
  /** The id of the panel the shown view is drawn in, labelled by {@link viewTabId} of that view. */
  panelId: string
  /** The tabs share the strip's width equally, as in the overview beside the steps. */
  fill?: boolean
}>()

const emit = defineEmits<{ select: [view: WorkflowView] }>()

/** Where each key moves from the tab at `index` of `count`, wrapping round at either end. */
const moves: Record<string, (index: number, count: number) => number> = {
  ArrowLeft: (index, count) => (index - 1 + count) % count,
  ArrowRight: (index, count) => (index + 1) % count,
  Home: () => 0,
  End: (_, count) => count - 1,
}

const onKeydown = (event: KeyboardEvent, index: number) => {
  const move = moves[event.key]
  if (!move) return
  event.preventDefault()
  const next = move(index, props.views.length)
  // From the DOM, not a template ref array, whose order Vue does not promise to match the views'.
  const strip = (event.currentTarget as HTMLElement).parentElement
  strip?.querySelectorAll<HTMLElement>('[role="tab"]')[next]?.focus()
  emit('select', props.views[next]!.value)
}
</script>

<template>
  <div
    aria-label="Overview view"
    class="ba-view-tabs"
    :class="{ 'ba-view-tabs-fill': props.fill }"
    role="tablist">
    <button
      v-for="(option, index) in props.views"
      :id="viewTabId(props.panelId, option.value)"
      :key="option.value"
      :aria-controls="props.panelId"
      :aria-selected="props.view === option.value"
      role="tab"
      :tabindex="props.view === option.value ? 0 : -1"
      type="button"
      @click="emit('select', option.value)"
      @keydown="onKeydown($event, index)">
      {{ option.label }}
    </button>
  </div>
</template>
