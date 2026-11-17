<script setup lang="ts">
import { computed } from 'vue'
import { formatValue } from '../helpers/format'
import { renderMarkdown } from '../helpers/markdown'
import { resolveReusables } from '../helpers/reusable'
import { resolveStepTarget, targetDescription, targetParameters } from '../helpers/resolve-target'
import { stepHeading } from '../helpers/heading'
import { specLabel, targetSpec } from '../helpers/target-spec'
import type { ArazzoDocument, ArazzoFailureAction, ArazzoParameter, ArazzoStep, ArazzoSuccessAction, PluginEventBus } from '../types'
import ArazzoActions from './ArazzoActions.vue'
import ArazzoAnchor from './ArazzoAnchor.vue'
import ArazzoKeyValues from './ArazzoKeyValues.vue'

const props = defineProps<{
  id: string
  step: ArazzoStep
  document: ArazzoDocument
  documents: Record<string, unknown>
  eventBus?: PluginEventBus
}>()

const target = computed(() => resolveStepTarget(props.document, props.step, props.documents))

/** Which spec the target is described in, so HTTP calls and messages can be told apart in a mixed workflow. */
const spec = computed(() => targetSpec(props.document, props.step, target.value, props.documents))

const specTitle = computed(() => {
  const value = spec.value
  if (!value) {
    return undefined
  }
  if (value.family === 'arazzo') {
    return 'Invokes a workflow of this Arazzo document'
  }
  const source = value.sourceName ? ` (source description "${value.sourceName}")` : ''
  return value.resolved
    ? `Described in ${specLabel(value)}${source}`
    : `${specLabel(value)}${source}, taken from the source description: the document is not loaded`
})

/** A one-line description reads as the step's title, as in Scalar's own POC; anything longer stays a description. */
const heading = computed(() => stepHeading(props.step))

/** The target's longer description, when it says more than the summary line above it. */
const targetDetails = computed(() => {
  const details = targetDescription(target.value)
  return details && details !== targetSummary.value ? renderMarkdown(details) : ''
})

const targetParameterRows = computed(() => targetParameters(target.value, props.documents))

/**
 * What an AsyncAPI step does on its channel. The step's own action (Arazzo 1.1 `channelPath` steps)
 * wins over the AsyncAPI operation's, and is known even when the target's document is not loaded.
 */
const channelAction = computed(() => {
  const value = target.value
  const action = props.step.action ?? (value?.kind === 'channel' ? value.operation?.action : undefined)
  return action === 'send' || action === 'receive' ? action : undefined
})

const targetLabel = computed(() => {
  switch (target.value?.kind) {
    case 'workflow':
      return 'Invokes workflow'
    case 'channel':
      return channelAction.value === 'send'
        ? 'Sends to channel'
        : channelAction.value === 'receive'
          ? 'Receives from channel'
          : 'Uses channel'
    default:
      return 'Calls operation'
  }
})

/** `GET /pets/{id}` for an OpenAPI target, the channel address for an AsyncAPI one. */
const targetSignature = computed(() => {
  const value = target.value
  if (value?.kind === 'operation' && value.method && value.path) {
    return `${value.method.toUpperCase()} ${value.path}`
  }
  if (value?.kind === 'channel' && value.channelName) {
    return value.channel?.address ?? value.channelName
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

const description = computed(() => renderMarkdown(heading.value.description))
</script>

<template>
  <section
    :id="id"
    class="ba-step"
    :data-nav-id="id">
    <div class="ba-kicker">Step · {{ step.stepId }}</div>
    <ArazzoAnchor
      :eventBus="eventBus"
      :navId="id">
      <h3
        class="ba-step-title"
        :class="{ 'ba-step-title-id': heading.title === step.stepId }">
        {{ heading.title }}
      </h3>
    </ArazzoAnchor>

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
      <span
        v-if="spec"
        class="ba-spec"
        :class="[`ba-spec-${spec.family}`, { 'ba-spec-unresolved': !spec.resolved }]"
        :title="specTitle">
        {{ spec.family === 'arazzo' ? 'Workflow' : specLabel(spec) }}
        <template v-if="!spec.resolved">· not loaded</template>
      </span>
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
      <div
        v-if="targetDetails"
        class="ba-markdown ba-target-summary"
        v-html="targetDetails" />
      <div
        v-if="targetParameterRows.length"
        class="ba-target-parameters">
        <div class="ba-block-title">{{ target.kind === 'channel' ? 'Channel parameters' : 'Operation parameters' }}</div>
        <table class="ba-table">
          <tbody>
            <tr
              v-for="parameter in targetParameterRows"
              :key="`${parameter.location}:${parameter.name}`">
              <td>
                <code>{{ parameter.name }}</code>
                <span
                  v-if="parameter.required"
                  class="ba-required">
                  required
                </span>
              </td>
              <td class="ba-muted">{{ parameter.location }}</td>
              <td class="ba-muted">{{ parameter.type }}</td>
              <td>{{ parameter.description }}</td>
            </tr>
          </tbody>
        </table>
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
