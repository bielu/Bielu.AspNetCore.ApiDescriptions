<script setup lang="ts">
import type { FlowValue } from '../helpers/data-flow-values'
import ArazzoSchemaProperties from './ArazzoSchemaProperties.vue'

/**
 * What one Data flow arrow carries: for each value, where it is made (the expression and the response,
 * message or inputs it reads), its schema as the loaded documents describe it, and where the receiving
 * side uses it.
 */
defineProps<{
  from: string
  to: string
  values: FlowValue[]
}>()
</script>

<template>
  <div class="ba-flow-details">
    <p class="ba-muted">
      From <strong>{{ from }}</strong> to <strong>{{ to }}</strong>
    </p>
    <section
      v-for="value in values"
      :key="value.name"
      class="ba-flow-value">
      <h4 class="ba-block-title">
        <code>{{ value.name }}</code>
      </h4>
      <dl class="ba-flow-facts">
        <dt>Set from</dt>
        <dd>
          <code>{{ value.source }}</code>
          <span
            v-if="value.origin"
            class="ba-muted">
            · {{ value.origin }}</span>
        </dd>
        <template v-if="value.uses.length">
          <dt>Used as</dt>
          <dd>{{ value.uses.join(', ') }}</dd>
        </template>
      </dl>
      <ArazzoSchemaProperties
        v-if="value.schema"
        :rows="[value.schema]" />
      <p
        v-else
        class="ba-muted ba-flow-no-schema">
        No schema found: the value's document isn't loaded or doesn't describe it.
      </p>
    </section>
  </div>
</template>
