<script setup lang="ts">
import { computed } from 'vue'
import { formatValue, schemaPropertyRows } from '../helpers/format'
import { renderMarkdown } from '../helpers/markdown'
import type { WorkflowView } from '../helpers/navigation'
import { resolveReusables } from '../helpers/reusable'
import type { ArazzoDocument, ArazzoFailureAction, ArazzoParameter, ArazzoSuccessAction } from '../types'
import ArazzoActions from './ArazzoActions.vue'
import ArazzoKeyValues from './ArazzoKeyValues.vue'
import ArazzoStep from './ArazzoStep.vue'

const props = defineProps<{
  view: WorkflowView
  document: ArazzoDocument
  documents: Record<string, unknown>
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
</script>

<template>
  <section
    :id="view.id"
    class="ba-workflow"
    :data-nav-id="view.id">
    <div class="ba-kicker">Workflow · {{ workflow.workflowId }}</div>
    <h2 class="ba-workflow-title">{{ workflow.summary || workflow.workflowId }}</h2>

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
      v-if="workflow.inputs"
      class="ba-block">
      <div class="ba-block-title">Inputs</div>
      <table
        v-if="inputRows"
        class="ba-table">
        <tbody>
          <tr
            v-for="row in inputRows"
            :key="row.name">
            <td>
              <code>{{ row.name }}</code>
              <span
                v-if="row.required"
                class="ba-required">
                required
              </span>
            </td>
            <td class="ba-muted">{{ row.type }}</td>
            <td>{{ row.description }}</td>
          </tr>
        </tbody>
      </table>
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
  </section>
</template>
