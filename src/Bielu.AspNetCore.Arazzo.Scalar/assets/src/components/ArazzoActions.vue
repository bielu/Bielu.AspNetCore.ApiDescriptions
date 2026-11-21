<script setup lang="ts">
import { resolveReusables } from '../helpers/reusable'
import type { ArazzoDocument, ArazzoFailureAction, ArazzoParameter, ArazzoSuccessAction } from '../types'
import ArazzoCriteria from './ArazzoCriteria.vue'
import ArazzoExtensions from './ArazzoExtensions.vue'
import ArazzoValue from './ArazzoValue.vue'

const props = defineProps<{
  title: string
  actions: (ArazzoSuccessAction | ArazzoFailureAction)[]
  /** The Arazzo document, so an action's Reusable Object parameters resolve against its components. */
  document?: ArazzoDocument
  /** Gives each action the id `<anchorPrefix>/<key>`, so a `$components` expression can link to it. */
  anchorPrefix?: string
  /** For component actions: each action's key in the components map, which the anchor uses; else its `name`. */
  keys?: string[]
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

/** The parameters an action passes to the workflow it invokes, Reusable Objects resolved. */
const parametersOf = (action: ArazzoSuccessAction | ArazzoFailureAction): ArazzoParameter[] =>
  props.document
    ? resolveReusables<ArazzoParameter>(props.document, action.parameters)
    : ((action.parameters ?? []).filter((entry) => 'name' in entry) as ArazzoParameter[])
</script>

<template>
  <div class="ba-block">
    <div class="ba-block-title">{{ title }}</div>
    <ul class="ba-actions">
      <li
        v-for="(action, index) in actions"
        :id="anchorPrefix ? `${anchorPrefix}/${keys?.[index] ?? action.name}` : undefined"
        :key="keys?.[index] ?? action.name"
        class="ba-anchor-target">
        <span class="ba-action-name">{{ action.name }}</span>
        <span class="ba-badge">{{ describe(action) }}</span>
        <ArazzoCriteria
          v-if="action.criteria?.length"
          :criteria="action.criteria"
          inline />
        <dl
          v-if="parametersOf(action).length"
          class="ba-action-parameters">
          <template
            v-for="parameter in parametersOf(action)"
            :key="parameter.name">
            <dt><code>{{ parameter.name }}</code></dt>
            <dd><ArazzoValue :value="parameter.value" /></dd>
          </template>
        </dl>
        <ArazzoExtensions :of="action" />
      </li>
    </ul>
  </div>
</template>
