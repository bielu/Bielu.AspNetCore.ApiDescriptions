<script setup lang="ts">
import { nextTick } from 'vue'
import type { OutputConsumer } from '../helpers/expressions'
import { goToAnchor } from '../helpers/navigation'
import ArazzoExpressionText from './ArazzoExpressionText.vue'
import ArazzoValue from './ArazzoValue.vue'

/** One row: a name, its constant, Runtime Expression or Selector Object, and optionally where it goes (`in`). */
export type KeyValueRow = {
  name: string
  value: unknown
  location?: string
  /** The `$components.parameters.<name>` reference the row was resolved from, if any. */
  reference?: string
  /** For an output: the steps (or workflow outputs) that read it. */
  usedBy?: OutputConsumer[]
}

defineProps<{
  title: string
  rows: KeyValueRow[]
  /** The id of the block; each row's is `<anchor>/<name>`, so a Runtime Expression can link to it. */
  anchor?: string
}>()

const follow = (event: MouseEvent, consumer: OutputConsumer) => {
  if (event.ctrlKey || event.metaKey || event.shiftKey || event.button !== 0) {
    return
  }
  event.preventDefault()
  void goToAnchor(event.currentTarget as Element, consumer, nextTick)
}
</script>

<template>
  <div
    :id="anchor"
    class="ba-block">
    <div class="ba-block-title">{{ title }}</div>
    <dl class="ba-key-values">
      <template
        v-for="(row, index) in rows"
        :key="index">
        <dt
          :id="anchor ? `${anchor}/${row.name}` : undefined"
          class="ba-anchor-target">
          {{ row.name }}
          <span
            v-if="row.location"
            class="ba-muted">
            {{ row.location }}
          </span>
          <span
            v-if="row.reference"
            class="ba-muted ba-row-reference">
            from <code><ArazzoExpressionText :text="row.reference" /></code>
          </span>
        </dt>
        <dd>
          <ArazzoValue :value="row.value" />
          <span
            v-if="row.usedBy?.length"
            class="ba-used-by">
            Used by
            <template
              v-for="(consumer, consumerIndex) in row.usedBy"
              :key="consumer.label">
              <template v-if="consumerIndex">, </template>
              <a
                v-if="consumer.anchor"
                :href="`#${consumer.anchor}`"
                @click="follow($event, consumer)">{{ consumer.label }}</a>
              <span v-else>{{ consumer.label }}</span>
            </template>
          </span>
        </dd>
      </template>
    </dl>
  </div>
</template>
