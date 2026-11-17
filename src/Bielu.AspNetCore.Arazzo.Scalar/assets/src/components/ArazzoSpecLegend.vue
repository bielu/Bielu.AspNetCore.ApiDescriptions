<script setup lang="ts">
import { computed } from 'vue'
import { specLabel, type SpecFamily } from '../helpers/target-spec'
import type { WorkflowLayout } from '../helpers/workflow-graph'

/** Explains the colored spec markers on the overview's step boxes; renders nothing when no step has one. */
const props = defineProps<{ layout: WorkflowLayout }>()

/** The specs the steps target, in a fixed order. */
const families = computed((): SpecFamily[] => {
  const used = new Set(props.layout.nodes.map((node) => node.spec).filter(Boolean))
  return (['openapi', 'asyncapi', 'arazzo'] as const).filter((family) => used.has(family))
})

const name = (family: SpecFamily): string => (family === 'arazzo' ? 'Workflow' : specLabel({ family, version: undefined }))
</script>

<template>
  <ul
    v-if="families.length"
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
  </ul>
</template>
