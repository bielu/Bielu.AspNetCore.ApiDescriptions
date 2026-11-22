<script setup lang="ts">
import { computed, onBeforeUnmount, onMounted, ref, watch } from 'vue'
import {
  criterionTypeLabel,
  formatDuration,
  formatValue,
  schemaPropertyRows,
} from '../helpers/format'
import {
  diagnosticKey,
  useDiagnostics,
  worstSeverity,
} from '../helpers/diagnostics-scope'
import { useExpressionScope } from '../helpers/expression-scope'
import { outputsAnchor, stepOutputConsumers } from '../helpers/expressions'
import { renderMarkdown } from '../helpers/markdown'
import {
  resolveReusables,
  resolveReusablesWithReference,
} from '../helpers/reusable'
import {
  resolveStepTarget,
  targetDescription,
  targetMessages,
  targetParameters,
} from '../helpers/resolve-target'
import { stepHeading } from '../helpers/heading'
import { STEP_EXPAND_EVENT } from '../helpers/navigation'
import { nestedWorkflowSteps } from '../helpers/nested-workflow'
import {
  navigationHref,
  openInOtherDocument,
  targetDisplayName,
  targetNavigationId,
} from '../helpers/target-link'
import { specLabel, targetSpec } from '../helpers/target-spec'
import type {
  ArazzoDocument,
  ArazzoFailureAction,
  ArazzoParameter,
  ArazzoStep,
  ArazzoSuccessAction,
  PluginEventBus,
} from '../types'
import ArazzoActions from './ArazzoActions.vue'
import ArazzoAnchor from './ArazzoAnchor.vue'
import ArazzoCriteria from './ArazzoCriteria.vue'
import ArazzoExpressionText from './ArazzoExpressionText.vue'
import ArazzoExtensions from './ArazzoExtensions.vue'
import ArazzoKeyValues from './ArazzoKeyValues.vue'
import ArazzoOperationExchange from './ArazzoOperationExchange.vue'
import ArazzoSchemaProperties from './ArazzoSchemaProperties.vue'
import ArazzoValue from './ArazzoValue.vue'

const props = defineProps<{
  id: string
  step: ArazzoStep
  document: ArazzoDocument
  documents: Record<string, unknown>
  eventBus?: PluginEventBus
  /** workflowId → the sidebar id of that workflow's section, so a step that invokes one can link to it. */
  workflowNavIds?: Record<string, string>
  /** Pages view: the page the step is on ("Order list · /orders") and its plain-language label. */
  page?: { page?: string; label?: string }
  /** "Collapse steps" / "Expand steps" of the workflow: each new `version` sets every step to `open`. */
  expandSignal?: { open: boolean; version: number }
}>()

/** Whether the step's body is shown. Steps start open; the chevron or the workflow's button folds them. */
const open = ref(true)
const bodyId = `${props.id}/body`
watch(
  () => props.expandSignal?.version,
  () => {
    if (props.expandSignal) {
      open.value = props.expandSignal.open
    }
  },
)

/** The overview diagram asks a step to open before it scrolls to it (`STEP_EXPAND_EVENT`). */
const section = ref<HTMLElement>()
const expand = (event: Event) => {
  // The event bubbles on to the workflow; it is this step's to handle here, not a nested one's.
  if (event.target === section.value) {
    open.value = true
  }
}
onMounted(() => section.value?.addEventListener(STEP_EXPAND_EVENT, expand))
onBeforeUnmount(() =>
  section.value?.removeEventListener(STEP_EXPAND_EVENT, expand),
)

const target = computed(() =>
  resolveStepTarget(props.document, props.step, props.documents),
)

/** What the target is called: its operationId or AsyncAPI name rather than the raw reference. */
const targetName = computed(() =>
  target.value ? targetDisplayName(target.value) : '',
)

/**
 * Where the target link goes: the operation or channel in its own document (a page load, since
 * Scalar picks the document on load), or the invoked workflow's section in this one.
 */
const targetLink = computed(
  (): { href: string; external: boolean; navId: string } | undefined => {
    const value = target.value
    if (value?.kind === 'workflow' && !value.documentName) {
      const navId = props.workflowNavIds?.[value.identifier]
      return navId ? { href: `#${navId}`, external: false, navId } : undefined
    }
    const navId = targetNavigationId(value, props.documents)
    return navId && typeof window !== 'undefined'
      ? { href: navigationHref(navId, window.location), external: true, navId }
      : undefined
  },
)

const followTargetLink = (event: MouseEvent) => {
  const link = targetLink.value
  if (!link) {
    return
  }
  if (link.external) {
    openInOtherDocument(event, link.href)
  } else if (
    !event.ctrlKey &&
    !event.metaKey &&
    !event.shiftKey &&
    event.button === 0
  ) {
    event.preventDefault()
    props.eventBus?.emit('scroll-to:nav-item', { id: link.navId })
  }
}

/** Which spec the target is described in, so HTTP calls and messages can be told apart in a mixed workflow. */
const spec = computed(() =>
  targetSpec(props.document, props.step, target.value, props.documents),
)

const specTitle = computed(() => {
  const value = spec.value
  if (!value) {
    return undefined
  }
  if (value.family === 'arazzo') {
    return 'Invokes a workflow of this Arazzo document'
  }
  const source = value.sourceName
    ? ` (source description "${value.sourceName}")`
    : ''
  return value.resolved
    ? `Described in ${specLabel(value)}${source}`
    : `${specLabel(value)}${source}, taken from the source description: the document is not loaded`
})

/** A one-line description reads as the step's title, as in Scalar's own POC; anything longer stays a description. */
const heading = computed(() => stepHeading(props.step))

/** The target's longer description, when it says more than the summary line above it. */
const targetDetails = computed(() => {
  const details = targetDescription(target.value)
  return details && details !== targetSummary.value
    ? renderMarkdown(details)
    : ''
})

const targetParameterRows = computed(() =>
  targetParameters(target.value, props.documents),
)

/**
 * The messages an AsyncAPI target sends or receives, each with its payload as a property list
 * (references followed in the AsyncAPI document), or as written, with its format, when it is in another
 * schema format: an Avro schema as JSON, a Protobuf one as its `.proto` source.
 */
const messages = computed(() =>
  targetMessages(target.value, props.documents).map((message) => ({
    ...message,
    rows: message.schemaFormat
      ? undefined
      : schemaPropertyRows(message.payload, message.root),
  })),
)

/** What a collapsed step still says: "Calls operation createOrder POST /orders". */
const collapsedSummary = computed(() => {
  const value = target.value
  if (!value) {
    return ''
  }
  const name = targetName.value || value.identifier
  const signature =
    targetSignature.value && targetSignature.value !== name
      ? ` ${targetSignature.value}`
      : ''
  return `${targetLabel.value} ${name}${signature}`
})

/** "Sends message", "Receives message", or just "Message" when the action is not known. */
const messageHeading = computed(() =>
  channelAction.value === 'send'
    ? 'Sends message'
    : channelAction.value === 'receive'
      ? 'Receives message'
      : 'Message',
)

/**
 * What an AsyncAPI step does on its channel. The step's own action (Arazzo 1.1 `channelPath` steps)
 * wins over the AsyncAPI operation's, and is known even when the target's document is not loaded.
 */
const channelAction = computed(() => {
  const value = target.value
  const action =
    props.step.action ??
    (value?.kind === 'channel' ? value.operation?.action : undefined)
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
  const source =
    value?.kind === 'operation'
      ? value.operation
      : (value?.operation ?? value?.channel)
  const summary = source?.summary ?? source?.title
  return typeof summary === 'string' ? summary : ''
})

/** The title of the sibling document the target lives in, falling back to its workspace name. */
const targetDocumentTitle = computed(() => {
  const value = target.value
  const name =
    value && value.kind !== 'workflow' ? value.documentName : undefined
  if (!name) {
    return undefined
  }
  const document = props.documents[name] as
    { info?: { title?: string } } | undefined
  return document?.info?.title || name
})

const parameters = computed(() =>
  resolveReusablesWithReference<ArazzoParameter>(
    props.document,
    props.step.parameters,
  ).map(({ entry, reference }) => ({
    name: entry.name,
    value: entry.value,
    location: entry.in,
    reference,
  })),
)

const expressionScope = useExpressionScope()

/** What looks wrong in this step, listed at the top of its body and counted beside its heading. */
const allDiagnostics = useDiagnostics()
const problems = computed(() => {
  const workflowId = expressionScope.value?.workflow?.workflowId
  return workflowId
    ? (allDiagnostics.value.get(diagnosticKey(workflowId, props.step.stepId)) ??
        [])
    : []
})
const severity = computed(() => worstSeverity(problems.value))

/** Each output with the steps (or workflow outputs) of the workflow that read it. */
const outputs = computed(() => {
  const scope = expressionScope.value
  const consumers = scope?.workflow
    ? stepOutputConsumers(
        { ...scope, workflow: scope.workflow },
        props.step.stepId,
      )
    : undefined
  return Object.entries(props.step.outputs ?? {}).map(([name, value]) => ({
    name,
    value,
    usedBy: consumers?.[name],
  }))
})

const onSuccess = computed(() =>
  resolveReusables<ArazzoSuccessAction>(props.document, props.step.onSuccess),
)

const onFailure = computed(() =>
  resolveReusables<ArazzoFailureAction>(props.document, props.step.onFailure),
)

const description = computed(() => renderMarkdown(heading.value.description))

/** For a step that invokes a workflow of this document: that workflow's steps, shown read-only on demand. */
const nested = computed(() =>
  nestedWorkflowSteps(props.document, props.step, props.documents),
)
const nestedOpen = ref(false)
const nestedId = `${props.id}/nested`
const nestedVerb = (kind: string): string =>
  kind === 'workflow'
    ? 'invokes workflow'
    : kind === 'channel'
      ? 'uses channel'
      : 'calls'
</script>

<template>
  <section
    :id="id"
    ref="section"
    class="ba-step"
    :class="{ 'ba-step-closed': !open }"
    :data-nav-id="id">
    <div class="ba-step-header">
      <button
        :aria-controls="bodyId"
        :aria-expanded="open"
        :aria-label="`${open ? 'Collapse' : 'Expand'} step ${step.stepId}`"
        class="ba-step-toggle"
        type="button"
        @click="open = !open">
        <svg
          aria-hidden="true"
          class="ba-chevron"
          viewBox="0 0 16 16">
          <path d="M6 4l4 4-4 4" />
        </svg>
      </button>
      <div class="ba-step-heading">
        <div
          v-if="page && (page.page || page.label)"
          class="ba-step-page">
          <!-- The space between badge and label is explicit: Vue drops whitespace between elements once it contains a newline. -->
          <span
            v-if="page.page"
            class="ba-badge ba-page-badge"
            >{{ page.page }}</span>{{ ' '
          }}<span v-if="page.label">{{ page.label }}</span>
        </div>
        <div
          v-if="open && heading.title !== step.stepId"
          class="ba-kicker ba-step-id">
          {{ step.stepId }}
        </div>
        <ArazzoAnchor
          :eventBus="eventBus"
          :navId="id">
          <h3
            class="ba-step-title"
            :class="{ 'ba-step-title-id': heading.title === step.stepId }">
            {{ heading.title }}
            <span
              v-if="severity"
              class="ba-step-problems"
              :class="`ba-diagnostic-${severity}`"
              :title="problems.map((problem) => problem.message).join('\n')">
              {{ severity === 'error' ? '✕' : '!' }} {{ problems.length }}
            </span>
          </h3>
        </ArazzoAnchor>
        <div
          v-if="!open && collapsedSummary"
          class="ba-step-collapsed-summary">
          <span
            v-if="spec"
            class="ba-spec"
            :class="[
              `ba-spec-${spec.family}`,
              { 'ba-spec-unresolved': !spec.resolved },
            ]">
            {{
              spec.family === 'arazzo'
                ? 'Workflow'
                : spec.family === 'openapi'
                  ? 'OpenAPI'
                  : 'AsyncAPI'
            }}
          </span>
          {{ collapsedSummary }}
        </div>
      </div>
    </div>

    <div
      v-show="open"
      :id="bodyId"
      class="ba-step-body">
      <ul
        v-if="problems.length"
        aria-label="Problems in this step"
        class="ba-step-diagnostics">
        <li
          v-for="(problem, index) in problems"
          :key="index"
          :class="`ba-diagnostic-${problem.severity}`">
          <span class="ba-diagnostic-severity">{{ problem.severity }}</span>
          <span>{{ problem.message }}</span>
        </li>
      </ul>
      <div
        v-if="
          step.dependsOn?.length ||
          typeof step.timeout === 'number' ||
          step.correlationId
        "
        class="ba-badges">
        <span
          v-for="dependency in step.dependsOn"
          :key="dependency"
          class="ba-badge">
          Depends on: {{ dependency }}
        </span>
        <span
          v-if="typeof step.timeout === 'number'"
          class="ba-badge"
          :title="`Fails when the step takes longer than ${step.timeout} ms`">
          Timeout: {{ formatDuration(step.timeout) }}
        </span>
        <span
          v-if="step.correlationId"
          class="ba-badge"
          title="Links the received message to the request it answers">
          Correlation ID:
          <code><ArazzoExpressionText :text="step.correlationId" /></code>
        </span>
      </div>

      <div
        v-if="description"
        class="ba-markdown"
        v-html="description" />

      <div
        v-if="target"
        class="ba-target"
        :class="{
          'ba-target-unresolved':
            target.kind !== 'workflow' && !target.documentName,
        }">
        <div class="ba-target-header">
          <span
            v-if="spec"
            class="ba-spec"
            :class="[
              `ba-spec-${spec.family}`,
              { 'ba-spec-unresolved': !spec.resolved },
            ]"
            :title="specTitle">
            {{
              spec.family === 'arazzo'
                ? 'Workflow'
                : spec.family === 'openapi'
                  ? 'OpenAPI'
                  : 'AsyncAPI'
            }}
            <template v-if="!spec.resolved">· not loaded</template>
          </span>
          <span
            v-if="target.kind !== 'operation'"
            class="ba-target-label"
            >{{
              channelAction ||
              (target.kind === 'workflow' ? 'Invoke' : 'Channel')
            }}</span
          >
          <a
            v-if="targetLink"
            class="ba-target-link"
            :href="targetLink.href"
            :title="
              targetLink.external
                ? `Open in ${targetDocumentTitle ?? 'its document'}`
                : 'Go to the workflow'
            "
            @click="followTargetLink">
            <code class="ba-target-signature">{{
              targetSignature || targetName || target.identifier
            }}</code>
            <span
              aria-hidden="true"
              class="ba-target-arrow">
              {{ targetLink.external ? '↗' : '↓' }}
            </span>
          </a>
          <template v-else>
            <code
              class="ba-target-signature"
              :title="target.identifier"
              >{{ targetSignature || targetName || target.identifier }}</code
            >
          </template>
        </div>
        <div
          v-if="
            (targetSignature && targetName !== targetSignature) ||
            targetDocumentTitle
          "
          class="ba-target-meta">
          <code
            v-if="
              targetSignature && targetName && targetName !== targetSignature
            "
            >{{ targetName }}</code
          >
          <span v-if="targetDocumentTitle">{{ targetDocumentTitle }}</span>
        </div>
        <div
          v-if="targetSummary"
          class="ba-target-summary">
          {{ targetSummary }}
        </div>
        <div
          v-if="nested"
          class="ba-nested">
          <button
            :aria-controls="nestedId"
            :aria-expanded="nestedOpen"
            class="ba-toggle"
            type="button"
            @click="nestedOpen = !nestedOpen">
            {{ nestedOpen ? 'Hide' : 'Show' }} its
            {{ nested.steps.length }} step{{
              nested.steps.length === 1 ? '' : 's'
            }}
          </button>
          <ol
            v-show="nestedOpen"
            :id="nestedId"
            :aria-label="`Steps of workflow ${nested.workflowId}`"
            class="ba-nested-steps">
            <li
              v-for="nestedStep in nested.steps"
              :key="nestedStep.stepId">
              <code>{{ nestedStep.stepId }}</code>
              <span class="ba-muted">{{ nestedVerb(nestedStep.kind) }}</span>
              <code>{{ nestedStep.target }}</code>
            </li>
          </ol>
        </div>
        <div
          v-if="targetDetails"
          class="ba-markdown ba-target-summary"
          v-html="targetDetails" />
        <div
          v-if="targetParameterRows.length"
          class="ba-target-parameters">
          <div class="ba-block-title">
            {{
              target.kind === 'channel'
                ? 'Channel parameters'
                : 'Operation parameters'
            }}
          </div>
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

        <ArazzoOperationExchange
          v-if="target.kind === 'operation'"
          :actions="[...onSuccess, ...onFailure]"
          :contentType="step.requestBody?.contentType"
          :documents="documents"
          :successCriteria="step.successCriteria"
          :target="target" />
        <div
          v-for="message in messages"
          :key="message.name"
          class="ba-target-message">
          <div class="ba-block-title">
            {{ messageHeading }}
            <code>{{ message.title || message.name }}</code>
            <span
              v-if="message.contentType"
              class="ba-muted">
              {{ message.contentType }}
            </span>
          </div>
          <div
            v-if="message.summary || message.description"
            class="ba-target-summary">
            {{ message.summary || message.description }}
          </div>
          <ArazzoSchemaProperties
            v-if="message.rows?.length"
            :rows="message.rows" />
          <template v-else-if="message.payload">
            <div
              v-if="message.schemaFormat"
              class="ba-muted">
              {{ message.schemaFormat }}
            </div>
            <pre
              class="ba-code"><code>{{ formatValue(message.payload) }}</code></pre>
          </template>
          <details
            v-if="message.example !== undefined"
            class="ba-extensions">
            <summary>Example payload</summary>
            <pre
              class="ba-code"><code>{{ formatValue(message.example) }}</code></pre>
          </details>
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
        <pre
          v-if="step.requestBody.payload !== undefined"
          class="ba-code"><code><ArazzoExpressionText :text="formatValue(step.requestBody.payload)" /></code></pre>
        <template v-if="step.requestBody.replacements?.length">
          <div class="ba-subtitle">Replacements</div>
          <table class="ba-table ba-replacements">
            <tbody>
              <tr
                v-for="(replacement, index) in step.requestBody.replacements"
                :key="index">
                <td>
                  <span
                    v-if="criterionTypeLabel(replacement.targetSelectorType)"
                    class="ba-badge ba-criterion-type">
                    {{ criterionTypeLabel(replacement.targetSelectorType) }}
                  </span>
                  <code>{{ replacement.target }}</code>
                </td>
                <td
                  aria-hidden="true"
                  class="ba-muted">
                  ←
                </td>
                <td><ArazzoValue :value="replacement.value" /></td>
              </tr>
            </tbody>
          </table>
        </template>
        <ArazzoExtensions :of="step.requestBody" />
      </div>

      <div
        v-if="step.successCriteria?.length"
        class="ba-block">
        <div class="ba-block-title">Success criteria</div>
        <ArazzoCriteria :criteria="step.successCriteria" />
      </div>

      <ArazzoActions
        v-if="onSuccess.length"
        :actions="onSuccess"
        :document="document"
        title="On success" />
      <ArazzoActions
        v-if="onFailure.length"
        :actions="onFailure"
        :document="document"
        title="On failure" />

      <ArazzoKeyValues
        v-if="outputs.length"
        :anchor="outputsAnchor(id)"
        :rows="outputs"
        title="Outputs" />

      <ArazzoExtensions :of="step" />
    </div>
  </section>
</template>
