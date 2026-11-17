<script setup lang="ts">
import { computed, ref } from 'vue'
import { formatValue, schemaPropertyRows } from '../helpers/format'
import { renderMarkdown } from '../helpers/markdown'
import type { WorkflowView } from '../helpers/navigation'
import { resolveReusables } from '../helpers/reusable'
import type { ArazzoDocument, ArazzoFailureAction, ArazzoParameter, ArazzoSuccessAction, PluginEventBus } from '../types'
import ArazzoActions from './ArazzoActions.vue'
import ArazzoAnchor from './ArazzoAnchor.vue'
import ArazzoKeyValues from './ArazzoKeyValues.vue'
import ArazzoSchemaProperties from './ArazzoSchemaProperties.vue'
import ArazzoStep from './ArazzoStep.vue'

const props = defineProps<{
  view: WorkflowView
  document: ArazzoDocument
  documents: Record<string, unknown>
  eventBus?: PluginEventBus
  /** Scalar's `layout` option: `classic` folds each workflow into a section that opens and closes. */
  layout?: string
}>()

const workflow = computed(() => props.view.workflow)

const description = computed(() => renderMarkdown(workflow.value.description))

const inputRows = computed(() => schemaPropertyRows(workflow.value.inputs))

const parameters = computed(() =>
  resolveReusables<ArazzoParameter>(props.document, workflow.value.parameters).map((parameter) => ({
    name: parameter.name,
    value: parameter.value,
    location: parameter.in,
  })),
)

const outputs = computed(() => Object.entries(workflow.value.outputs ?? {}).map(([name, value]) => ({ name, value })))

const successActions = computed(() =>
  resolveReusables<ArazzoSuccessAction>(props.document, workflow.value.successActions),
)

const failureActions = computed(() =>
  resolveReusables<ArazzoFailureAction>(props.document, workflow.value.failureActions),
)

const classic = computed(() => props.layout === 'classic')

/** Classic layout only: whether the workflow's body is shown. Open by default, so the sidebar's step links land on content. */
const open = ref(true)

const toggle = () => {
  open.value = !open.value
  props.eventBus?.emit('toggle:nav-item', { id: props.view.id, open: open.value })
}
</script>

<template>
  <section
    :id="view.id"
    class="ba-workflow"
    :class="{ 'ba-workflow-classic': classic, 'ba-workflow-closed': classic && !open }"
    :data-nav-id="view.id">
    <div class="ba-workflow-header">
      <div class="ba-workflow-heading">
        <div class="ba-kicker">Workflow · {{ workflow.workflowId }}</div>
        <ArazzoAnchor
          :eventBus="eventBus"
          :navId="view.id">
          <h2 class="ba-workflow-title">{{ workflow.summary || workflow.workflowId }}</h2>
        </ArazzoAnchor>
      </div>
      <button
        v-if="classic"
        :aria-expanded="open"
        class="ba-toggle"
        type="button"
        @click="toggle">
        {{ open ? 'Collapse' : 'Expand' }}
      </button>
    </div>

    <div
      v-if="workflow.dependsOn?.length"
      class="ba-badges">
      <span
        v-for="dependency in workflow.dependsOn"
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
      v-show="!classic || open"
      class="ba-workflow-content">
      <div
        v-if="workflow.inputs"
        class="ba-block">
        <div class="ba-block-title">Inputs</div>
        <ArazzoSchemaProperties
          v-if="inputRows"
          :rows="inputRows" />
        <pre
          v-else
          class="ba-code"><code>{{ formatValue(workflow.inputs) }}</code></pre>
      </div>

      <ArazzoKeyValues
        v-if="parameters.length"
        :rows="parameters"
        title="Parameters (all steps)" />

      <ol class="ba-steps">
        <li
          v-for="entry in view.steps"
          :key="entry.id">
          <ArazzoStep
            :id="entry.id"
            :document="document"
            :documents="documents"
            :eventBus="eventBus"
            :step="entry.step" />
        </li>
      </ol>

      <ArazzoActions
        v-if="successActions.length"
        :actions="successActions"
        title="Success actions (all steps)" />
      <ArazzoActions
        v-if="failureActions.length"
        :actions="failureActions"
        title="Failure actions (all steps)" />

      <ArazzoKeyValues
        v-if="outputs.length"
        :rows="outputs"
        title="Outputs" />
    </div>
  </section>
</template>
