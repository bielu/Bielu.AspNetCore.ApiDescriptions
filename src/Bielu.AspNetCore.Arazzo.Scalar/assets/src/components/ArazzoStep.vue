<script setup lang="ts">
import { computed } from 'vue'
import { formatValue } from '../helpers/format'
import { renderMarkdown } from '../helpers/markdown'
import { resolveReusables } from '../helpers/reusable'
import { resolveStepTarget } from '../helpers/resolve-target'
import type { ArazzoDocument, ArazzoFailureAction, ArazzoParameter, ArazzoStep, ArazzoSuccessAction } from '../types'
import ArazzoActions from './ArazzoActions.vue'
import ArazzoKeyValues from './ArazzoKeyValues.vue'

const props = defineProps<{
  id: string
  step: ArazzoStep
  document: ArazzoDocument
  documents: Record<string, unknown>
}>()

const target = computed(() => resolveStepTarget(props.document, props.step, props.documents))

const targetLabel = computed(() => {
  switch (target.value?.kind) {
    case 'workflow':
      return 'Invokes workflow'
    case 'channel':
      return 'Channel'
    default:
      return 'Operation'
  }
})

/** `GET /pets/{id}` for an OpenAPI target, the channel name for an AsyncAPI one. */
const targetSignature = computed(() => {
  const value = target.value
  if (value?.kind === 'operation' && value.method && value.path) {
    return `${value.method.toUpperCase()} ${value.path}`
  }
  if (value?.kind === 'channel') {
    // The step's own action (Arazzo 1.1 channelPath steps) wins over the AsyncAPI operation's.
    const action = props.step.action ?? value.operation?.action
    const prefix = typeof action === 'string' ? `${action.toUpperCase()} ` : ''
    return value.channelName ? `${prefix}${value.channel?.address ?? value.channelName}` : undefined
  }
  return undefined
})

const targetSummary = computed((): string => {
  const value = target.value
  if (value?.kind === 'workflow') {
    return value.workflow?.summary ?? ''
  }
  const source = value?.kind === 'operation' ? value.operation : (value?.operation ?? value?.channel)
  const summary = source?.summary ?? source?.title
  return typeof summary === 'string' ? summary : ''
})

/** The title of the sibling document the target lives in, falling back to its workspace name. */
const targetDocumentTitle = computed(() => {
  const value = target.value
  const name = value && value.kind !== 'workflow' ? value.documentName : undefined
  if (!name) {
    return undefined
  }
  const document = props.documents[name] as { info?: { title?: string } } | undefined
  return document?.info?.title || name
})

const parameters = computed(() =>
  resolveReusables<ArazzoParameter>(props.document, props.step.parameters).map((parameter) => ({
    name: parameter.name,
    value: parameter.value,
    location: parameter.in,
  })),
)

const outputs = computed(() => Object.entries(props.step.outputs ?? {}).map(([name, value]) => ({ name, value })))

const onSuccess = computed(() => resolveReusables<ArazzoSuccessAction>(props.document, props.step.onSuccess))

const onFailure = computed(() => resolveReusables<ArazzoFailureAction>(props.document, props.step.onFailure))

const description = computed(() => renderMarkdown(props.step.description))
</script>

<template>
  <section
    :id="id"
    class="ba-step"
    :data-nav-id="id">
    <div class="ba-kicker">Step</div>
    <h3 class="ba-step-title">{{ step.stepId }}</h3>

    <div
      v-if="step.dependsOn?.length"
      class="ba-badges">
      <span
        v-for="dependency in step.dependsOn"
        :key="dependency"
        class="ba-badge">
        Depends on: {{ dependency }}
      </span>
    </div>

    <div
      v-if="description"
      class="ba-markdown"
      v-html="description" />

    <div
      v-if="target"
      class="ba-target"
      :class="{ 'ba-target-unresolved': target.kind !== 'workflow' && !target.documentName }">
      <span class="ba-target-label">{{ targetLabel }}</span>
      <code class="ba-target-id">{{ target.identifier }}</code>
      <span
        v-if="targetSignature"
        class="ba-target-signature">
        {{ targetSignature }}
      </span>
      <span
        v-if="targetDocumentTitle"
        class="ba-muted">
        in {{ targetDocumentTitle }}
      </span>
      <div
        v-if="targetSummary"
        class="ba-target-summary">
        {{ targetSummary }}
      </div>
    </div>

    <ArazzoKeyValues
      v-if="parameters.length"
      :rows="parameters"
      title="Parameters" />

    <div
      v-if="step.requestBody"
      class="ba-block">
      <div class="ba-block-title">
        Request body
        <span
          v-if="step.requestBody.contentType"
          class="ba-muted">
          {{ step.requestBody.contentType }}
        </span>
      </div>
      <pre class="ba-code"><code>{{ formatValue(step.requestBody.payload) }}</code></pre>
    </div>

    <div
      v-if="step.successCriteria?.length"
      class="ba-block">
      <div class="ba-block-title">Success criteria</div>
      <ul class="ba-criteria">
        <li
          v-for="(criterion, index) in step.successCriteria"
          :key="index">
          <code>{{ criterion.condition }}</code>
          <span
            v-if="criterion.context"
            class="ba-muted">
            on {{ criterion.context }}
          </span>
        </li>
      </ul>
    </div>

    <ArazzoActions
      v-if="onSuccess.length"
      :actions="onSuccess"
      title="On success" />
    <ArazzoActions
      v-if="onFailure.length"
      :actions="onFailure"
      title="On failure" />

    <ArazzoKeyValues
      v-if="outputs.length"
      :rows="outputs"
      title="Outputs" />
  </section>
</template>
