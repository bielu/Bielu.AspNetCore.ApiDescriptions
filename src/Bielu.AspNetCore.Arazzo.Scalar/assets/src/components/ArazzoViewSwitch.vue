<script setup lang="ts">
import type { WorkflowView } from '../helpers/workflow-graph'

/**
 * The overview's views as tabs on a row of their own, so every view's name stays on one line however narrow the
 * overview is; a strip too narrow for them all scrolls sideways.
 */
const props = defineProps<{
  views: { value: WorkflowView; label: string }[]
  view: WorkflowView
  /** The tabs share the strip's width equally, as in the overview beside the steps. */
  fill?: boolean
}>()

const emit = defineEmits<{ select: [view: WorkflowView] }>()
</script>

<template>
  <div
    aria-label="Overview view"
    class="ba-view-tabs"
    :class="{ 'ba-view-tabs-fill': props.fill }"
    role="group">
    <button
      v-for="option in props.views"
      :key="option.value"
      :aria-pressed="props.view === option.value"
      type="button"
      @click="emit('select', option.value)">{{ option.label }}</button>
  </div>
</template>
