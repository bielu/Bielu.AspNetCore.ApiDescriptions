<script setup lang="ts">
import type { ArazzoFailureAction, ArazzoSuccessAction } from '../types'

defineProps<{
  title: string
  actions: (ArazzoSuccessAction | ArazzoFailureAction)[]
}>()

/** "goto step x", "retry (≤ 3×, after 2s)", "end" — the part of an action a reader scans for. */
const describe = (action: ArazzoSuccessAction | ArazzoFailureAction): string => {
  const target = action.stepId ? `step ${action.stepId}` : action.workflowId ? `workflow ${action.workflowId}` : ''
  const retry = action as ArazzoFailureAction
  const limits = [
    typeof retry.retryLimit === 'number' ? `≤ ${retry.retryLimit}×` : '',
    typeof retry.retryAfter === 'number' ? `after ${retry.retryAfter}s` : '',
  ].filter(Boolean)
  return [action.type, target, limits.length ? `(${limits.join(', ')})` : ''].filter(Boolean).join(' ')
}
</script>

<template>
  <div class="ba-block">
    <div class="ba-block-title">{{ title }}</div>
    <ul class="ba-actions">
      <li
        v-for="action in actions"
        :key="action.name">
        <span class="ba-action-name">{{ action.name }}</span>
        <span class="ba-badge">{{ describe(action) }}</span>
        <code
          v-for="(criterion, index) in action.criteria ?? []"
          :key="index"
          class="ba-condition">
          {{ criterion.condition }}
        </code>
      </li>
    </ul>
  </div>
</template>
