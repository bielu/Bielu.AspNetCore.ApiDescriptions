<script setup lang="ts">
import ArazzoValue from './ArazzoValue.vue'

/** One row: a name, its constant, Runtime Expression or Selector Object, and optionally where it goes (`in`). */
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
        <dd><ArazzoValue :value="row.value" /></dd>
      </template>
    </dl>
  </div>
</template>
