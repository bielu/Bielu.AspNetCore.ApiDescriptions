<script setup lang="ts">
import { computed, nextTick } from 'vue'
import { diagnosticSummary, type Diagnostic } from '../helpers/diagnostics'
import { goToAnchor } from '../helpers/navigation'

/**
 * The document's diagnostics in its header: a count of errors and warnings that folds out into the list, each
 * entry linking to the step (or workflow) it is about.
 */
const props = defineProps<{ diagnostics: Diagnostic[] }>()

const summary = computed(() => diagnosticSummary(props.diagnostics))

/** Errors first, then warnings, each in document order. */
const sorted = computed(() => [
  ...props.diagnostics.filter((diagnostic) => diagnostic.severity === 'error'),
  ...props.diagnostics.filter((diagnostic) => diagnostic.severity === 'warning'),
])

const where = (diagnostic: Diagnostic): string => [diagnostic.workflowId, diagnostic.stepId].filter(Boolean).join(' / ')

const follow = (event: MouseEvent, diagnostic: Diagnostic) => {
  if (event.ctrlKey || event.metaKey || event.shiftKey || event.button !== 0) {
    return
  }
  event.preventDefault()
  void goToAnchor(event.currentTarget as Element, { anchor: diagnostic.anchor, stepSection: diagnostic.stepId ? diagnostic.anchor : undefined }, nextTick)
}
</script>

<template>
  <details
    v-if="summary"
    class="ba-diagnostics"
    :class="sorted[0]?.severity === 'error' ? 'ba-diagnostics-error' : 'ba-diagnostics-warning'">
    <summary>
      <span
        aria-hidden="true"
        class="ba-diagnostic-icon">{{ sorted[0]?.severity === 'error' ? '✕' : '!' }}</span>
      {{ summary }} found in this document
    </summary>
    <ul class="ba-diagnostic-list">
      <li
        v-for="(diagnostic, index) in sorted"
        :key="index"
        :class="`ba-diagnostic-${diagnostic.severity}`">
        <span class="ba-diagnostic-severity">{{ diagnostic.severity }}</span>
        <a
          v-if="diagnostic.anchor"
          class="ba-diagnostic-where"
          :href="`#${diagnostic.anchor}`"
          @click="follow($event, diagnostic)">{{ where(diagnostic) }}</a>
        <span
          v-else
          class="ba-diagnostic-where">{{ where(diagnostic) }}</span>
        <span>{{ diagnostic.message }}</span>
      </li>
    </ul>
  </details>
</template>
