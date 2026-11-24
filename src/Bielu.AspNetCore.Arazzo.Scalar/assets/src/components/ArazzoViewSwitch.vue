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
  /**
   * Where the tabs sit. `overview`: beside the steps, sharing the overview's width equally under its own line.
   * `dialog`: in the expanded workflow's tab bar, which draws the line, at their own width and a size larger.
   */
  placement: 'overview' | 'dialog'
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
    class="ba-view-tabs gap-1 overflow-x-auto overflow-y-hidden"
    :class="
      props.placement === 'overview'
        ? 'ba-view-tabs-fill flex w-full border-b border-b-border'
        : 'inline-flex'
    "
    role="tablist">
    <button
      v-for="(option, index) in props.views"
      :id="viewTabId(props.panelId, option.value)"
      :key="option.value"
      :aria-controls="props.panelId"
      :aria-selected="props.view === option.value"
      class="-mb-px cursor-pointer border-b-2 border-b-transparent bg-transparent whitespace-nowrap text-c-2 hover:text-c-1 focus-visible:rounded focus-visible:outline-2 focus-visible:-outline-offset-2 focus-visible:outline-c-accent aria-selected:border-b-c-accent aria-selected:text-c-1"
      :class="
        props.placement === 'overview'
          ? 'flex-1 px-2 py-1 text-(length:--scalar-mini,12px)'
          : 'px-3 py-1.5 text-(length:--scalar-small,13px)'
      "
      role="tab"
      :tabindex="props.view === option.value ? 0 : -1"
      type="button"
      @click="emit('select', option.value)"
      @keydown="onKeydown($event, index)">
      {{ option.label }}
    </button>
  </div>
</template>
