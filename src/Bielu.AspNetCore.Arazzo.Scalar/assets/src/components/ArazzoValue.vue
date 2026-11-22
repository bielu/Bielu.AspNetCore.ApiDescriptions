<script setup lang="ts">
import { Badge as ScalarBadge } from '@scalar/blocks/shared'
import { computed } from 'vue'
import { asSelector, criterionTypeLabel, formatValue } from '../helpers/format'
import ArazzoExpressionText from './ArazzoExpressionText.vue'

/**
 * One value of an Arazzo document: a constant, a Runtime Expression, or (Arazzo 1.1) a Selector
 * Object, which reads as "`$.items[0].id` on `$response.body`" with its language as a badge rather
 * than as raw JSON.
 */
const props = defineProps<{ value: unknown }>()

const selector = computed(() => asSelector(props.value))
</script>

<template>
  <span
    v-if="selector"
    class="ba-selector">
    <ScalarBadge
      v-if="criterionTypeLabel(selector.type)"
      class="ba-badge ba-criterion-type">
      {{ criterionTypeLabel(selector.type) }}
    </ScalarBadge>
    <code>{{ selector.selector }}</code>
    <span class="ba-muted"> on </span>
    <code><ArazzoExpressionText :text="selector.context" /></code>
  </span>
  <code v-else><ArazzoExpressionText :text="formatValue(value)" /></code>
</template>
