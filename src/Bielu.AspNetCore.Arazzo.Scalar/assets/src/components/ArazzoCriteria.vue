<script setup lang="ts">
import { criterionTypeLabel } from '../helpers/format'
import type { ArazzoCriterion } from '../types'
import ArazzoExpressionText from './ArazzoExpressionText.vue'

/**
 * A list of criteria, all of which must hold: each condition, the language it is written in when it
 * is not a simple condition (`regex`, `jsonpath`, `xpath`), and the context it is evaluated against.
 * `inline` renders them in a row, as an action's conditions are.
 */
defineProps<{
  criteria: ArazzoCriterion[]
  inline?: boolean
}>()
</script>

<template>
  <ul
    class="ba-criteria"
    :class="{ 'ba-criteria-inline': inline }">
    <li
      v-for="(criterion, index) in criteria"
      :key="index">
      <span
        v-if="criterionTypeLabel(criterion.type)"
        class="ba-badge ba-criterion-type">
        {{ criterionTypeLabel(criterion.type) }}
      </span>
      <code class="ba-condition"
        ><ArazzoExpressionText :text="criterion.condition"
      /></code>
      <span
        v-if="criterion.context"
        class="ba-muted">
        on <code><ArazzoExpressionText :text="criterion.context" /></code>
      </span>
    </li>
  </ul>
</template>
