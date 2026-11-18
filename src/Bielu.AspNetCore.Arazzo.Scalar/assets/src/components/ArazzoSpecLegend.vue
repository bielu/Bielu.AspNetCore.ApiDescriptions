<script setup lang="ts">
import { computed } from 'vue'
import { specLabel, type SpecFamily } from '../helpers/target-spec'
import type { WorkflowLayout } from '../helpers/workflow-graph'

/**
 * Explains the colored spec markers on the overview's step boxes, and the workflow nodes and retry
 * arrows when the overview has them; renders nothing when there is nothing to explain.
 */
const props = defineProps<{ layout: WorkflowLayout }>()

/** The specs the steps target, in a fixed order. */
const families = computed((): SpecFamily[] => {
  const used = new Set(props.layout.nodes.map((node) => node.spec).filter(Boolean))
  return (['openapi', 'asyncapi', 'arazzo'] as const).filter((family) => used.has(family))
})

/** The other marks a reader meets in the overview, listed only when it uses them. */
const workflowNodes = computed(() => props.layout.nodes.some((node) => node.kind === 'workflow'))
const expandable = computed(() => props.layout.nodes.some((node) => node.expandable))
const returns = computed(() => props.layout.edges.some((edge) => edge.kind === 'return'))

const name = (family: SpecFamily): string => (family === 'arazzo' ? 'Workflow' : specLabel({ family, version: undefined }))
</script>

<template>
  <ul
    v-if="families.length || workflowNodes"
    aria-label="Specs the steps target"
    class="ba-diagram-legend">
    <li
      v-for="family in families"
      :key="family">
      <span
        class="ba-diagram-legend-swatch"
        :class="`ba-spec-${family}`" />
      {{ name(family) }}
    </li>
    <li v-if="workflowNodes">
      <span class="ba-diagram-legend-workflow" />
      Workflow call<template v-if="expandable"> (+ shows its steps)</template>
    </li>
    <li v-if="returns">
      <span
        class="ba-diagram-legend-line"
        style="--ba-line-color: var(--scalar-color-orange, #d97706)" />
      Back after the retry
    </li>
  </ul>
</template>
