<script setup lang="ts">
import { computed } from 'vue'
import { extensionsOf } from '../helpers/extensions'
import ArazzoValue from './ArazzoValue.vue'

/**
 * The `x-*` Specification Extensions of one Arazzo object, folded away by default: they matter to the
 * tools that read them, rarely to someone learning the workflow. Renders nothing when there are none.
 */
const props = defineProps<{ of: unknown }>()

const entries = computed(() => extensionsOf(props.of))
</script>

<template>
  <details
    v-if="entries.length"
    class="ba-extensions">
    <summary>Extensions ({{ entries.length }})</summary>
    <dl class="ba-key-values">
      <template
        v-for="[name, value] in entries"
        :key="name">
        <dt><code>{{ name }}</code></dt>
        <dd><ArazzoValue :value="value" /></dd>
      </template>
    </dl>
  </details>
</template>
