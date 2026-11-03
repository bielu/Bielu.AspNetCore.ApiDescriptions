<script setup lang="ts">
import { formatValue } from '../helpers/format'

/** One row: a name, its constant or Runtime Expression value, and optionally where it goes (`in`). */
export type KeyValueRow = {
  name: string
  value: unknown
  location?: string
}

defineProps<{
  title: string
  rows: KeyValueRow[]
}>()
</script>

<template>
  <div class="ba-block">
    <div class="ba-block-title">{{ title }}</div>
    <dl class="ba-key-values">
      <template
        v-for="(row, index) in rows"
        :key="index">
        <dt>
          {{ row.name }}
          <span
            v-if="row.location"
            class="ba-muted">
            {{ row.location }}
          </span>
        </dt>
        <dd><code>{{ formatValue(row.value) }}</code></dd>
      </template>
    </dl>
  </div>
</template>
