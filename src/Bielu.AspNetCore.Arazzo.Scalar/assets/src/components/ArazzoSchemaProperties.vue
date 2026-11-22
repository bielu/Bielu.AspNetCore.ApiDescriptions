<script setup lang="ts">
import type { SchemaPropertyRow } from '../helpers/format'
import { renderMarkdown } from '../helpers/markdown'

/** A JSON Schema's properties as a list; an object property's own properties fold out beneath it. */
defineProps<{
  rows: SchemaPropertyRow[]
  /** Gives each top-level property the id `<anchorPrefix>/<name>`, so a Runtime Expression can link to it. */
  anchorPrefix?: string
}>()
</script>

<template>
  <ul class="ba-schema">
    <li
      v-for="row in rows"
      :id="anchorPrefix ? `${anchorPrefix}/${row.name}` : undefined"
      :key="row.name"
      class="ba-schema-property ba-anchor-target">
      <div class="ba-schema-heading">
        <code class="ba-schema-name">{{ row.name }}</code>
        <span class="ba-schema-type">{{ row.type }}</span>
        <span
          v-for="detail in row.details ?? []"
          :key="detail"
          class="ba-schema-detail">
          {{ detail }}
        </span>
        <span
          v-if="row.required"
          class="ba-required">
          required
        </span>
      </div>
      <div
        v-if="row.description"
        class="ba-markdown ba-schema-description"
        v-html="renderMarkdown(row.description)" />
      <details
        v-if="row.children?.length"
        class="ba-schema-children">
        <summary>
          Show {{ row.children.length }} child
          {{ row.children.length === 1 ? 'property' : 'properties' }}
        </summary>
        <ArazzoSchemaProperties :rows="row.children" />
      </details>
    </li>
  </ul>
</template>
