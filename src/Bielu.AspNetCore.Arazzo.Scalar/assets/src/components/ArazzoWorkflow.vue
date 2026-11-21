<script setup lang="ts">
import { computed, nextTick, onBeforeUnmount, onMounted, ref } from 'vue'
import { diagnosticKey, useDiagnostics, worstSeverity } from '../helpers/diagnostics-scope'
import { provideExpressionScope } from '../helpers/expression-scope'
import { inputsAnchor, outputsAnchor, workflowOutputConsumers } from '../helpers/expressions'
import { formatValue, schemaPropertyRows } from '../helpers/format'
import { resolveLocalRef } from '../helpers/extensions'
import { renderMarkdown } from '../helpers/markdown'
import { STEP_EXPAND_EVENT, type WorkflowView } from '../helpers/navigation'
import { resolveReusables, resolveReusablesWithReference } from '../helpers/reusable'
import { labelOf, pageOf, pageRegistry, pagesInUse, pageTitle, type PagesConfig } from '../helpers/pages'
import type { ArazzoPluginOptions } from '../helpers/plugin-options'
import { resolveStepTarget } from '../helpers/resolve-target'
import { targetSpec } from '../helpers/target-spec'
import { setWorkflowView, workflowView } from '../helpers/view-mode'
import { buildWorkflowGraph, layoutWorkflowGraph, type WorkflowView as OverviewView } from '../helpers/workflow-graph'
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
  arazzoOptions?: ArazzoPluginOptions
  view: WorkflowView
  document: ArazzoDocument
  documents: Record<string, unknown>
  eventBus?: PluginEventBus
  /** Scalar's `layout` option: `classic` folds each workflow into a section that opens and closes. */
  layout?: string
  /** workflowId → the sidebar id of that workflow's section. */
  workflowNavIds?: Record<string, string>
  /** The steps a filter leaves on screen, by sidebar id; every step when absent. */
  visibleSteps?: Set<string>
  /**
   * workflowId → stepId → section id for every workflow of the document, so a step inside an expanded workflow in
   * the overview opens that workflow's step. Only this workflow's steps when absent.
   */
  stepSectionIds?: Record<string, Record<string, string>>
  /** The workspace name of this Arazzo document, so the overview knows a workflow of another document that calls back into it. */
  documentName?: string
  /** The components section's sidebar id, so `$components` expressions link to it. */
  componentsId?: string
}>()

const workflow = computed(() => props.view.workflow)

const description = computed(() => renderMarkdown(workflow.value.description))

/** The inputs schema, followed through a `$ref` to `components.inputs` when it is one. */
const inputs = computed(() => resolveLocalRef(props.document, workflow.value.inputs))

const inputRows = computed(() => schemaPropertyRows(inputs.value))

const parameters = computed(() =>
  resolveReusablesWithReference<ArazzoParameter>(props.document, workflow.value.parameters).map(({ entry, reference }) => ({
    name: entry.name,
    value: entry.value,
    location: entry.in,
    reference,
  })),
)

/** Each output with the steps of other workflows that read it (`$workflows.<id>.outputs.<name>`). */
const outputs = computed(() => {
  const consumers = workflowOutputConsumers(expressionScope.value)
  return Object.entries(workflow.value.outputs ?? {}).map(([name, value]) => ({ name, value, usedBy: consumers[name] }))
})

const successActions = computed(() =>
  resolveReusables<ArazzoSuccessAction>(props.document, workflow.value.successActions),
)

const failureActions = computed(() =>
  resolveReusables<ArazzoFailureAction>(props.document, workflow.value.failureActions),
)

/** The workflow nodes whose steps the overview shows; shared by the inline and the expanded diagram. */
const expandedWorkflows = ref<ReadonlySet<string>>(new Set())

const toggleWorkflow = (nodeId: string) => {
  const next = new Set(expandedWorkflows.value)
  if (next.has(nodeId)) {
    // Hiding a workflow hides what was expanded inside it, so showing it again starts collapsed.
    for (const id of next) {
      if (id === nodeId || id.startsWith(`${nodeId}/`)) {
        next.delete(id)
      }
    }
  } else {
    next.add(nodeId)
  }
  expandedWorkflows.value = next
}

/** Where the Pages view finds pages and labels (the plugin's options). */
const pagesConfig = computed((): PagesConfig => {
  const options = props.arazzoOptions ?? {}
  return {
    pagesExtension: options.pagesExtension,
    pageExtension: options.pageExtension,
    labelExtension: options.labelExtension,
    endLabelExtension: options.endLabelExtension,
  }
})

/** The Technical | Pages toggle is offered only when the pages options are set and this document uses them. */
const pagesAvailable = computed(() => pagesInUse(props.document, pagesConfig.value))

const overviewView = computed((): OverviewView => (pagesAvailable.value ? workflowView() : 'technical'))

const views: { value: OverviewView; label: string }[] = [
  { value: 'technical', label: 'Technical' },
  { value: 'pages', label: 'Pages' },
]

/** Pages view: each step's page (title and route) and plain-language label, for the step list. */
const stepPages = computed(() => {
  if (overviewView.value !== 'pages') {
    return {}
  }
  const registry = pageRegistry(props.document, pagesConfig.value)
  return Object.fromEntries(
    props.view.steps.map(({ step }) => {
      const page = pageOf(step, pagesConfig.value)
      const route = page ? registry[page]?.route : undefined
      return [step.stepId, { page: page ? [pageTitle(registry, page), route].filter(Boolean).join(' · ') : undefined, label: labelOf(step, pagesConfig.value) }]
    }),
  )
})

/** Each step box is marked with the spec its target is described in. */
const diagram = computed(() =>
  layoutWorkflowGraph(
    buildWorkflowGraph(
      props.document,
      workflow.value,
      // A step of an expanded workflow from another Arazzo document resolves its target in that document.
      (step, owner) => targetSpec(owner, step, resolveStepTarget(owner, step, props.documents), props.documents)?.family,
      {
        expanded: expandedWorkflows.value,
        endLabelExtension: props.arazzoOptions?.endLabelExtension,
        view: overviewView.value,
        pages: pagesConfig.value,
        documents: props.documents,
        documentName: props.documentName,
      },
    ),
  ),
)

const stepSectionIds = computed(
  () => props.stepSectionIds ?? { [workflow.value.workflowId]: Object.fromEntries(props.view.steps.map((entry) => [entry.step.stepId, entry.id])) },
)

/** The scope the workflow's Runtime Expressions are read in, for every value, condition and payload under it. */
const expressionScope = computed(() => ({
  document: props.document,
  workflow: workflow.value,
  workflowNavIds: { ...props.workflowNavIds, [workflow.value.workflowId]: props.view.id },
  stepSectionIds: stepSectionIds.value,
  componentsId: props.componentsId,
}))
provideExpressionScope(() => expressionScope.value)

/** stepId → the worst problem in that step, marked on its box in the overview. */
const allDiagnostics = useDiagnostics()
const stepProblems = computed(() =>
  Object.fromEntries(
    props.view.steps.flatMap(({ step }) => {
      const problems = allDiagnostics.value.get(diagnosticKey(workflow.value.workflowId, step.stepId))
      const severity = worstSeverity(problems)
      return severity ? [[step.stepId, { severity, count: problems!.length, messages: problems!.map((problem) => problem.message) }]] : []
    }),
  ),
)

const classic = computed(() => props.layout === 'classic')

/** Classic layout only: whether the workflow's body is shown. Open by default, so the sidebar's step links land on content. */
const open = ref(true)

const toggle = () => {
  open.value = !open.value
  props.eventBus?.emit('toggle:nav-item', { id: props.view.id, open: open.value })
}

/** A step of this workflow asked to open (`STEP_EXPAND_EVENT` bubbling up from it): show the workflow's body too. */
const root = ref<HTMLElement>()
const openForStep = () => {
  if (classic.value && !open.value) {
    toggle()
  }
}
onMounted(() => root.value?.addEventListener(STEP_EXPAND_EVENT, openForStep))
onBeforeUnmount(() => root.value?.removeEventListener(STEP_EXPAND_EVENT, openForStep))

/**
 * Folds or unfolds every step of the workflow at once; each step can still be toggled on its own
 * afterwards. A new `version` is what the steps react to, so pressing the button twice works.
 */
const expandSignal = ref({ open: true, version: 0 })
const toggleSteps = () => {
  expandSignal.value = { open: !expandSignal.value.open, version: expandSignal.value.version + 1 }
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
    ref="root"
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
      <div class="ba-workflow-actions">
        <button
          v-if="view.steps.length"
          class="ba-toggle"
          type="button"
          @click="toggleSteps">
          {{ expandSignal.open ? 'Collapse steps' : 'Expand steps' }}
        </button>
        <button
          v-if="classic"
          :aria-expanded="open"
          class="ba-toggle"
          type="button"
          @click="toggle">
          {{ open ? 'Collapse' : 'Expand' }}
        </button>
      </div>
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
          :id="inputsAnchor(view.id)"
          class="ba-block ba-anchor-target">
          <div class="ba-block-title">Inputs</div>
          <ArazzoSchemaProperties
            v-if="inputRows"
            :anchorPrefix="inputsAnchor(view.id)"
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
            v-for="(entry, index) in view.steps"
            v-show="!visibleSteps || visibleSteps.has(entry.id)"
            :key="entry.id"
            :data-step-number="index + 1">
            <ArazzoStep
              :id="entry.id"
              :document="document"
              :documents="documents"
              :eventBus="eventBus"
              :expandSignal="expandSignal"
              :page="stepPages[entry.step.stepId]"
              :step="entry.step"
              :workflowNavIds="workflowNavIds" />
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
          :anchor="outputsAnchor(view.id)"
          :rows="outputs"
          title="Outputs" />
      </div>

      <aside
        v-if="diagram.nodes.length"
        class="ba-workflow-aside">
        <div class="ba-overview">
          <div class="ba-overview-header">
            <div class="ba-overview-title">Workflow overview</div>
            <div
              v-if="pagesAvailable"
              aria-label="Overview view"
              class="ba-view-toggle"
              role="group">
              <button
                v-for="option in views"
                :key="option.value"
                :aria-pressed="overviewView === option.value"
                type="button"
                @click="setWorkflowView(option.value)">{{ option.label }}</button>
            </div>
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
            :problems="stepProblems"
            :stepSectionIds="stepSectionIds"
            :workflowId="workflow.workflowId"
            @toggleWorkflow="toggleWorkflow" />
          <div class="ba-overview-footer">
            <ArazzoSpecLegend
              :layout="diagram"
              :view="overviewView" />
            <span class="ba-muted ba-overview-hint">Select a step to view details</span>
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
          <div
            v-if="pagesAvailable"
            aria-label="Overview view"
            class="ba-view-toggle"
            role="group">
            <button
              v-for="option in views"
              :key="option.value"
              :aria-pressed="overviewView === option.value"
              type="button"
              @click="setWorkflowView(option.value)">{{ option.label }}</button>
          </div>
          <ArazzoSpecLegend
            :layout="diagram"
            :view="overviewView" />
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
        :problems="stepProblems"
        :stepSectionIds="stepSectionIds"
        :workflowId="workflow.workflowId"
        @openStep="dialog?.close()"
        @toggleWorkflow="toggleWorkflow" />
    </dialog>
  </section>
</template>
