<script setup lang="ts">
import { computed, nextTick, ref } from 'vue'
import { formatValue, schemaPropertyRows } from '../helpers/format'
import { resolveLocalRef } from '../helpers/extensions'
import { renderMarkdown } from '../helpers/markdown'
import type { WorkflowView } from '../helpers/navigation'
import { resolveReusables } from '../helpers/reusable'
import { resolveStepTarget } from '../helpers/resolve-target'
import { targetSpec } from '../helpers/target-spec'
import { buildWorkflowGraph, layoutWorkflowGraph } from '../helpers/workflow-graph'
import type { ArazzoDocument, ArazzoFailureAction, ArazzoParameter, ArazzoSuccessAction, PluginEventBus } from '../types'
import ArazzoActions from './ArazzoActions.vue'
import ArazzoAnchor from './ArazzoAnchor.vue'
import ArazzoExtensions from './ArazzoExtensions.vue'
import ArazzoKeyValues from './ArazzoKeyValues.vue'
import ArazzoSchemaProperties from './ArazzoSchemaProperties.vue'
import ArazzoSpecLegend from './ArazzoSpecLegend.vue'
import ArazzoStep from './ArazzoStep.vue'
import ArazzoWorkflowDiagram from './ArazzoWorkflowDiagram.vue'

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

/** The inputs schema, followed through a `$ref` to `components.inputs` when it is one. */
const inputs = computed(() => resolveLocalRef(props.document, workflow.value.inputs))

const inputRows = computed(() => schemaPropertyRows(inputs.value))

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

/** Each step box is marked with the spec its target is described in. */
const diagram = computed(() =>
  layoutWorkflowGraph(
    buildWorkflowGraph(
      props.document,
      workflow.value,
      (step) => targetSpec(props.document, step, resolveStepTarget(props.document, step, props.documents), props.documents)?.family,
    ),
  ),
)

const stepSectionIds = computed(() => Object.fromEntries(props.view.steps.map((entry) => [entry.step.stepId, entry.id])))

const classic = computed(() => props.layout === 'classic')

/** Classic layout only: whether the workflow's body is shown. Open by default, so the sidebar's step links land on content. */
const open = ref(true)

const toggle = () => {
  open.value = !open.value
  props.eventBus?.emit('toggle:nav-item', { id: props.view.id, open: open.value })
}

/** The expanded overview, in a native `<dialog>`: it brings the backdrop, Escape to close, and focus handling. */
const dialog = ref<HTMLDialogElement>()
const expanded = ref<InstanceType<typeof ArazzoWorkflowDiagram>>()

const expand = async () => {
  dialog.value?.showModal()
  await nextTick()
  expanded.value?.resetView()
}

const closeOnBackdrop = (event: MouseEvent) => {
  if (event.target === dialog.value) {
    dialog.value?.close()
  }
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
    <ArazzoExtensions :of="workflow" />

    <div
      v-show="!classic || open"
      class="ba-workflow-content">
      <div class="ba-workflow-main">
        <div
          v-if="workflow.inputs"
          class="ba-block">
          <div class="ba-block-title">Inputs</div>
          <ArazzoSchemaProperties
            v-if="inputRows"
            :rows="inputRows" />
          <pre
            v-else
            class="ba-code"><code>{{ formatValue(inputs) }}</code></pre>
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
          :document="document"
          title="Success actions (all steps)" />
        <ArazzoActions
          v-if="failureActions.length"
          :actions="failureActions"
          :document="document"
          title="Failure actions (all steps)" />

        <ArazzoKeyValues
          v-if="outputs.length"
          :rows="outputs"
          title="Outputs" />
      </div>

      <aside
        v-if="diagram.nodes.length"
        class="ba-workflow-aside">
        <div class="ba-overview">
          <div class="ba-overview-header">
            <div class="ba-overview-title">Workflow overview</div>
            <button
              aria-label="Expand workflow diagram"
              class="ba-icon-button"
              title="Expand"
              type="button"
              @click="expand">
              <svg
                aria-hidden="true"
                fill="none"
                height="14"
                stroke="currentColor"
                stroke-linecap="round"
                stroke-width="1.6"
                viewBox="0 0 16 16"
                width="14">
                <path d="M2 6V2h4M14 6V2h-4M2 10v4h4M14 10v4h-4" />
              </svg>
            </button>
          </div>
          <ArazzoWorkflowDiagram
            :layout="diagram"
            :stepSectionIds="stepSectionIds" />
          <div class="ba-overview-footer">
            <ArazzoSpecLegend :layout="diagram" />
            <div class="ba-muted ba-overview-hint">Drag to pan, scroll to zoom, double-click to reset.</div>
          </div>
        </div>
      </aside>
    </div>

    <dialog
      ref="dialog"
      :aria-label="`${workflow.summary || workflow.workflowId} overview`"
      class="ba-dialog"
      @click="closeOnBackdrop">
      <div class="ba-dialog-header">
        <div class="ba-overview-title">Workflow overview · {{ workflow.summary || workflow.workflowId }}</div>
        <div class="ba-dialog-actions">
          <ArazzoSpecLegend :layout="diagram" />
          <button
            class="ba-toggle"
            type="button"
            @click="expanded?.resetView()">
            Reset view
          </button>
          <button
            aria-label="Close"
            class="ba-icon-button"
            title="Close"
            type="button"
            @click="dialog?.close()">
            <svg
              aria-hidden="true"
              fill="none"
              height="14"
              stroke="currentColor"
              stroke-linecap="round"
              stroke-width="1.6"
              viewBox="0 0 16 16"
              width="14">
              <path d="M3 3l10 10M13 3L3 13" />
            </svg>
          </button>
        </div>
      </div>
      <ArazzoWorkflowDiagram
        ref="expanded"
        class="ba-diagram-large"
        fit
        :layout="diagram"
        :stepSectionIds="stepSectionIds"
        @click.capture="(event: MouseEvent) => (event.target as Element).closest('.ba-diagram-link') && dialog?.close()" />
    </dialog>
  </section>
</template>
