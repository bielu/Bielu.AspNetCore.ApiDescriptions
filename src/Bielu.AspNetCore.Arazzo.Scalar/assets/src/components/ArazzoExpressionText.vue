<script setup lang="ts">
import { computed, nextTick } from 'vue'
import { useExpressionScope } from '../helpers/expression-scope'
import {
  resolveExpression,
  splitExpressions,
  type ExpressionTarget,
} from '../helpers/expressions'
import { goToAnchor } from '../helpers/navigation'

/**
 * A text with the Runtime Expressions in it marked: one that reads an input, a step's or workflow's
 * output, or a component links to it; one that names something the document does not declare is
 * marked broken, with the reason in its tooltip. Meant to sit inside a `<code>` or `<pre>`.
 */
const props = defineProps<{ text: string }>()

const scope = useExpressionScope()

const segments = computed(() =>
  splitExpressions(props.text).map((segment) => ({
    text: segment.text,
    target:
      segment.expression && scope.value
        ? resolveExpression(segment.text, scope.value)
        : undefined,
  })),
)

const follow = (event: MouseEvent, target: ExpressionTarget) => {
  if (event.ctrlKey || event.metaKey || event.shiftKey || event.button !== 0) {
    return
  }
  event.preventDefault()
  void goToAnchor(event.currentTarget as Element, target, nextTick)
}
</script>

<template>
  <template
    v-for="(segment, index) in segments"
    :key="index">
    <a
      v-if="segment.target?.anchor && !segment.target.broken"
      class="ba-expression ba-expression-link"
      :href="`#${segment.target.anchor}`"
      :title="segment.target.title"
      @click="follow($event, segment.target)"
      >{{ segment.text }}</a
    >
    <span
      v-else-if="segment.target?.broken"
      class="ba-expression ba-expression-broken"
      :title="segment.target.broken"
      >{{ segment.text }}</span
    >
    <span
      v-else-if="segment.target"
      class="ba-expression"
      :title="segment.target.title"
      >{{ segment.text }}</span
    >
    <template v-else>{{ segment.text }}</template>
  </template>
</template>
