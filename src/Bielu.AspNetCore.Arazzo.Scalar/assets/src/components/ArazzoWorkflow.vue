<script setup lang="ts">
import { ScalarModal, useModal } from '@scalar/components/modal'
import {
  computed,
  nextTick,
  onBeforeUnmount,
  onMounted,
  ref,
  useId,
  watch,
} from 'vue'
import {
  diagnosticKey,
  useDiagnostics,
  worstSeverity,
} from '../helpers/diagnostics-scope'
import { provideExpressionScope } from '../helpers/expression-scope'
import {
  inputsAnchor,
  outputsAnchor,
  workflowOutputConsumers,
} from '../helpers/expressions'
import { exampleWithCheck } from '../helpers/example-input'
import { formatValue, schemaPropertyRows } from '../helpers/format'
import { resolveLocalRef } from '../helpers/extensions'
import { renderMarkdown } from '../helpers/markdown'
import { STEP_EXPAND_EVENT, type WorkflowView } from '../helpers/navigation'
import { buildDataFlowGraph } from '../helpers/data-flow'
import { buildSequence } from '../helpers/sequence'
import { describeFlow } from '../helpers/data-flow-values'
import {
  resolveReusables,
  resolveReusablesWithReference,
} from '../helpers/reusable'
import {
  labelOf,
  pageOf,
  pageRegistry,
  pagesInUse,
  pageTitle,
  type PagesConfig,
} from '../helpers/pages'
import type { ArazzoPluginOptions } from '../helpers/plugin-options'
import { resolveStepTarget } from '../helpers/resolve-target'
import { describeStep } from '../helpers/step-description'
import { targetSpec } from '../helpers/target-spec'
import { setWorkflowView, workflowView } from '../helpers/view-mode'
import {
  buildWorkflowGraph,
  layoutWorkflowGraph,
  type LaidOutEdge,
  type WorkflowView as OverviewView,
} from '../helpers/workflow-graph'
import type {
  ArazzoDocument,
  ArazzoFailureAction,
  ArazzoParameter,
  ArazzoStep as StepObject,
  ArazzoSuccessAction,
  PluginEventBus,
} from '../types'
import ArazzoActions from './ArazzoActions.vue'
import ArazzoAnchor from './ArazzoAnchor.vue'
import ArazzoDataFlowDetails from './ArazzoDataFlowDetails.vue'
import ArazzoDiagramExport from './ArazzoDiagramExport.vue'
import ArazzoSequenceDiagram from './ArazzoSequenceDiagram.vue'
import ArazzoExtensions from './ArazzoExtensions.vue'
import ArazzoKeyValues from './ArazzoKeyValues.vue'
import ArazzoSchemaProperties from './ArazzoSchemaProperties.vue'
import ArazzoSpecLegend from './ArazzoSpecLegend.vue'
import ArazzoStep from './ArazzoStep.vue'
import ArazzoViewSwitch, { viewTabId } from './ArazzoViewSwitch.vue'
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
const inputs = computed(() =>
  resolveLocalRef(props.document, workflow.value.inputs),
)

const inputRows = computed(() => schemaPropertyRows(inputs.value))

/** A sample of what to pass the workflow, made from its inputs schema, to copy and edit. */
const example = computed(() => exampleWithCheck(inputs.value, props.document))

const exampleInput = computed(() =>
  example.value === undefined
    ? undefined
    : JSON.stringify(example.value.value, null, 2),
)

/** "Copied" for a moment after the copy button, read out to screen readers. */
const copyStatus = ref('')
const copyExample = async () => {
  try {
    await navigator.clipboard.writeText(exampleInput.value ?? '')
    copyStatus.value = 'Copied'
  } catch {
    copyStatus.value = 'Select the text to copy it'
  }
  setTimeout(() => (copyStatus.value = ''), 2000)
}

const parameters = computed(() =>
  resolveReusablesWithReference<ArazzoParameter>(
    props.document,
    workflow.value.parameters,
  ).map(({ entry, reference }) => ({
    name: entry.name,
    value: entry.value,
    location: entry.in,
    reference,
  })),
)

/** Each output with the steps of other workflows that read it (`$workflows.<id>.outputs.<name>`). */
const outputs = computed(() => {
  const consumers = workflowOutputConsumers(expressionScope.value)
  return Object.entries(workflow.value.outputs ?? {}).map(([name, value]) => ({
    name,
    value,
    usedBy: consumers[name],
  }))
})

const successActions = computed(() =>
  resolveReusables<ArazzoSuccessAction>(
    props.document,
    workflow.value.successActions,
  ),
)

const failureActions = computed(() =>
  resolveReusables<ArazzoFailureAction>(
    props.document,
    workflow.value.failureActions,
  ),
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
const pagesAvailable = computed(() =>
  pagesInUse(props.document, pagesConfig.value),
)

/** The reader's view; Pages falls back to Technical where this document has no pages. */
const overviewView = computed((): OverviewView => {
  const chosen = workflowView()
  return chosen === 'pages' && !pagesAvailable.value ? 'technical' : chosen
})

/** Technical and Data flow always; Pages when the document describes its pages. */
const views = computed((): { value: OverviewView; label: string }[] => [
  { value: 'technical', label: 'Technical' },
  { value: 'data', label: 'Data flow' },
  { value: 'sequence', label: 'Sequence' },
  ...(pagesAvailable.value
    ? [{ value: 'pages' as const, label: 'Pages' }]
    : []),
])

/** The panels the view tabs control, beside the steps and in the expanded overview; unique per workflow on the page. */
const viewPanelId = `ba-view-${useId()}`
const inlinePanelId = `${viewPanelId}-inline`
const dialogPanelId = `${viewPanelId}-dialog`

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
      return [
        step.stepId,
        {
          page: page
            ? [pageTitle(registry, page), route].filter(Boolean).join(' · ')
            : undefined,
          label: labelOf(step, pagesConfig.value),
        },
      ]
    }),
  )
})

/** Sequence view: who the workflow talks to, and in which order. */
const sequence = computed(() =>
  buildSequence(props.document, workflow.value, props.documents),
)

/** stepId → section id, for the sequence diagram's step names. */
const ownStepSections = computed(() =>
  Object.fromEntries(
    props.view.steps.map((entry) => [entry.step.stepId, entry.id]),
  ),
)

/** The spec a step's target is described in, resolved in the document the step belongs to. */
const specOf = (step: StepObject, owner: ArazzoDocument) =>
  targetSpec(
    owner,
    step,
    resolveStepTarget(owner, step, props.documents),
    props.documents,
  )?.family

/** Each step box is marked with the spec its target is described in. */
const diagram = computed(() =>
  overviewView.value === 'data'
    ? layoutWorkflowGraph(
        buildDataFlowGraph(props.document, workflow.value, specOf),
      )
    : layoutWorkflowGraph(
        // A step of an expanded workflow from another Arazzo document resolves its target in that document.
        buildWorkflowGraph(props.document, workflow.value, specOf, {
          expanded: expandedWorkflows.value,
          endLabelExtension: props.arazzoOptions?.endLabelExtension,
          view: overviewView.value,
          pages: pagesConfig.value,
          documents: props.documents,
          documentName: props.documentName,
          // Each box says what its step does (`POST /orders`, `← receives orderPlaced`), in its own document.
          describeStep: (step, owner) =>
            describeStep(step, owner, props.documents),
        }),
      ),
)

const stepSectionIds = computed(
  () =>
    props.stepSectionIds ?? {
      [workflow.value.workflowId]: Object.fromEntries(
        props.view.steps.map((entry) => [entry.step.stepId, entry.id]),
      ),
    },
)

/** The scope the workflow's Runtime Expressions are read in, for every value, condition and payload under it. */
const expressionScope = computed(() => ({
  document: props.document,
  workflow: workflow.value,
  workflowNavIds: {
    ...props.workflowNavIds,
    [workflow.value.workflowId]: props.view.id,
  },
  stepSectionIds: stepSectionIds.value,
  componentsId: props.componentsId,
}))
provideExpressionScope(() => expressionScope.value)

/** stepId → the worst problem in that step, marked on its box in the overview. */
const allDiagnostics = useDiagnostics()
const stepProblems = computed(() =>
  Object.fromEntries(
    props.view.steps.flatMap(({ step }) => {
      const problems = allDiagnostics.value.get(
        diagnosticKey(workflow.value.workflowId, step.stepId),
      )
      const severity = worstSeverity(problems)
      return severity
        ? [
            [
              step.stepId,
              {
                severity,
                count: problems!.length,
                messages: problems!.map((problem) => problem.message),
              },
            ],
          ]
        : []
    }),
  ),
)

const classic = computed(() => props.layout === 'classic')

/** Classic layout only: whether the workflow's body is shown. Open by default, so the sidebar's step links land on content. */
const open = ref(true)

const toggle = () => {
  open.value = !open.value
  props.eventBus?.emit('toggle:nav-item', {
    id: props.view.id,
    open: open.value,
  })
}

/** A step of this workflow asked to open (`STEP_EXPAND_EVENT` bubbling up from it): show the workflow's body too. */
const root = ref<HTMLElement>()
const openForStep = () => {
  if (classic.value && !open.value) {
    toggle()
  }
}
onMounted(() => root.value?.addEventListener(STEP_EXPAND_EVENT, openForStep))
onBeforeUnmount(() =>
  root.value?.removeEventListener(STEP_EXPAND_EVENT, openForStep),
)

/**
 * Folds or unfolds every step of the workflow at once; each step can still be toggled on its own
 * afterwards. A new `version` is what the steps react to, so pressing the button twice works.
 */
const expandSignal = ref({ open: true, version: 0 })
const toggleSteps = () => {
  expandSignal.value = {
    open: !expandSignal.value.open,
    version: expandSignal.value.version + 1,
  }
}

/**
 * The expanded overview, in Scalar's modal: it brings the backdrop, Escape and click-outside to close, and focus
 * handling. Its content is only rendered while it is open, in a portal at the end of <body>.
 */
const modal = useModal()
const expanded = ref<InstanceType<typeof ArazzoWorkflowDiagram>>()
const inline = ref<InstanceType<typeof ArazzoWorkflowDiagram>>()

// Another view draws another graph, which the scale measured for the last one may not fit: measure it again,
// keeping the reader's zoom. Only when the view changes, not the layout, so expanding a workflow keeps its scale.
watch(
  overviewView,
  () => {
    inline.value?.remeasure()
    expanded.value?.remeasure()
  },
  { flush: 'post' },
)

const expand = async () => {
  modal.show()
  // The modal renders its content on the next update; the diagram measures itself once it is there.
  await nextTick()
  await nextTick()
  expanded.value?.resetView()
}

/** Opening a step from the full-window sequence (pointer or keyboard) closes the window, as a box in the full-window overview does. */
const onSequenceNavigate = () => modal.hide()

/** Data flow view: the arrow whose values are shown in the flow dialog, over the overview (inline or full window). */
const flowEdge = ref<LaidOutEdge>()
const flowDialog = ref<HTMLDialogElement>()
/**
 * The full-window overview's content, while it is open. The flow dialog moves into it then: ScalarModal treats a
 * click or focus anywhere outside its own content as leaving it, and would close under the dialog.
 */
const modalContent = ref<HTMLElement>()

const flowDetails = computed(() => {
  const edge = flowEdge.value
  if (!edge?.values?.length) {
    return undefined
  }
  const label = (id: string) =>
    diagram.value.nodes.find((node) => node.id === id)?.label ?? id
  return {
    from: label(edge.from),
    to: label(edge.to),
    values: describeFlow(
      props.document,
      workflow.value,
      edge.from,
      edge.to,
      edge.values,
      props.documents,
      props.view.id,
    ),
  }
})

const openFlow = async (edge: LaidOutEdge) => {
  flowEdge.value = edge
  await nextTick()
  flowDialog.value?.showModal()
}

/** Following a link from the flow dialog closes it, and the full-window overview under it. */
const closeFlow = () => {
  flowDialog.value?.close()
  modal.hide()
}

const closeFlowOnBackdrop = (event: MouseEvent) => {
  if (event.target === flowDialog.value) {
    flowDialog.value?.close()
  }
}
</script>

<template>
  <section
    :id="view.id"
    ref="root"
    class="ba-workflow"
    :class="{
      'ba-workflow-classic': classic,
      'ba-workflow-closed': classic && !open,
    }"
    :data-nav-id="view.id">
    <div class="ba-workflow-header flex items-start justify-between gap-4">
      <div class="ba-workflow-heading min-w-0">
        <div class="ba-kicker">Workflow · {{ workflow.workflowId }}</div>
        <ArazzoAnchor
          :eventBus="eventBus"
          :navId="view.id">
          <h2 class="ba-workflow-title">
            {{ workflow.summary || workflow.workflowId }}
          </h2>
        </ArazzoAnchor>
      </div>
      <div class="ba-workflow-actions flex flex-wrap justify-end gap-1.5">
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

    <!-- The steps, with the overview beside them once the view is 960px wide; it stays in sight while they scroll. -->
    <div
      v-show="!classic || open"
      class="ba-workflow-content grid [grid-template-columns:minmax(0,1fr)] [align-items:start] gap-8 @min-[960px]/bielu-arazzo:grid-cols-[minmax(0,3fr)_minmax(0,2fr)]">
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
          <details
            v-if="exampleInput"
            class="ba-extensions ba-example-input">
            <summary>Example input</summary>
            <div class="ba-example-actions mx-0 my-1.5 flex items-center gap-2">
              <button
                class="ba-toggle"
                type="button"
                @click="copyExample">
                Copy
              </button>
              <span
                aria-live="polite"
                class="ba-muted"
                >{{ copyStatus }}</span
              >
            </div>
            <p
              v-if="example?.needsEdits"
              class="ba-muted ba-example-warning mx-0 my-1.5">
              Some values don't satisfy the inputs schema (a pattern or length
              it can't meet): edit them before use.
            </p>
            <pre class="ba-code"><code>{{ exampleInput }}</code></pre>
          </details>
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
        class="ba-workflow-aside mt-4 min-w-0">
        <div
          class="ba-overview rounded-lg border border-border bg-b-1 p-3 @min-[960px]/bielu-arazzo:sticky @min-[960px]/bielu-arazzo:top-[calc(var(--refs-viewport-offset,0px)+24px)]">
          <!-- The tabs have a row of their own: the header is the title, and the actions kept together on the right. -->
          <div
            class="ba-overview-header mb-2 flex items-center justify-between gap-2">
            <div
              class="ba-overview-title mr-auto text-(length:--scalar-mini) font-bold tracking-[normal] text-c-2">
              Workflow overview
            </div>
            <ArazzoDiagramExport
              v-if="overviewView !== 'sequence'"
              :layout="diagram"
              :name="workflow.workflowId"
              :svg="() => inline?.svg()" />
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
          <ArazzoViewSwitch
            class="ba-overview-tabs mb-2"
            :panelId="inlinePanelId"
            placement="overview"
            :view="overviewView"
            :views="views"
            @select="setWorkflowView" />
          <div
            :id="inlinePanelId"
            :aria-labelledby="viewTabId(inlinePanelId, overviewView)"
            role="tabpanel">
            <ArazzoSequenceDiagram
              v-if="overviewView === 'sequence'"
              :sequence="sequence"
              :stepSectionIds="ownStepSections" />
            <ArazzoWorkflowDiagram
              v-else
              ref="inline"
              :layout="diagram"
              :problems="stepProblems"
              :stepSectionIds="stepSectionIds"
              :workflowId="workflow.workflowId"
              @openFlow="openFlow"
              @toggleWorkflow="toggleWorkflow" />
          </div>
          <!-- Under the diagram rather than over it, so it never hides a step box. -->
          <div
            class="ba-overview-footer mt-1.5 flex flex-wrap items-baseline justify-between gap-x-3 gap-y-1">
            <ArazzoSpecLegend
              v-if="overviewView !== 'sequence'"
              :layout="diagram"
              :view="overviewView" />
            <span
              class="ba-muted ba-overview-hint mt-0 text-(length:--scalar-mini)"
              >Select a step to view details</span
            >
          </div>
        </div>
      </aside>
    </div>

    <!--
      Portalled out of the view, so the content carries the view's root class for its styles, and undoes the page
      layout that class gives the view's root.
    -->
    <ScalarModal
      :aria-label="`${workflow.summary || workflow.workflowId} overview`"
      bodyClass="ba-dialog-body"
      class="bielu-arazzo-modal"
      maxWidth="96vw"
      size="xl"
      :state="modal">
      <div
        ref="modalContent"
        class="bielu-arazzo ba-dialog m-0 flex h-full max-w-none flex-col p-0">
        <div
          class="ba-dialog-header mb-2 flex items-center justify-between gap-2">
          <div
            class="ba-overview-title text-(length:--scalar-mini) font-bold tracking-[normal] text-c-2">
            Workflow overview · {{ workflow.summary || workflow.workflowId }}
          </div>
          <div class="ba-dialog-actions flex items-center gap-1.5">
            <ArazzoDiagramExport
              v-if="overviewView !== 'sequence'"
              :layout="diagram"
              :name="workflow.workflowId"
              :svg="() => expanded?.svg()" />
            <button
              v-if="overviewView !== 'sequence'"
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
              @click="modal.hide()">
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
        <!-- A tab bar under the title: the views on the left, what the diagram's marks mean on the right. -->
        <div
          class="ba-dialog-tab-bar mb-2 flex items-end justify-between gap-4 border-b border-b-border">
          <ArazzoViewSwitch
            :panelId="dialogPanelId"
            placement="dialog"
            :view="overviewView"
            :views="views"
            @select="setWorkflowView" />
          <ArazzoSpecLegend
            v-if="overviewView !== 'sequence'"
            :layout="diagram"
            :view="overviewView" />
        </div>
        <!-- The rest of the window, for the diagram to fill. -->
        <div
          :id="dialogPanelId"
          :aria-labelledby="viewTabId(dialogPanelId, overviewView)"
          class="ba-dialog-panel flex min-h-0 flex-1 flex-col"
          role="tabpanel">
          <ArazzoSequenceDiagram
            v-if="overviewView === 'sequence'"
            fit
            :sequence="sequence"
            :stepSectionIds="ownStepSections"
            @navigate="onSequenceNavigate" />
          <ArazzoWorkflowDiagram
            v-else
            ref="expanded"
            class="ba-diagram-large max-h-none min-h-0 flex-1 items-center p-6"
            fit
            :layout="diagram"
            :problems="stepProblems"
            :stepSectionIds="stepSectionIds"
            :workflowId="workflow.workflowId"
            @openFlow="openFlow"
            @openStep="modal.hide()"
            @toggleWorkflow="toggleWorkflow" />
        </div>
      </div>
    </ScalarModal>

    <!--
      Escape closes this dialog only: kept from the window, where ScalarModal would close too. It is sized to what
      flows: m-auto centers it again after Scalar's reset, and a modal dialog is fixed with inset 0, so h-fit keeps
      it from stretching to the window.
    -->
    <Teleport
      :disabled="!modalContent"
      :to="modalContent">
      <dialog
        ref="flowDialog"
        :aria-label="
          flowDetails
            ? `What flows from ${flowDetails.from} to ${flowDetails.to}`
            : 'What flows'
        "
        class="ba-dialog ba-flow-dialog m-auto h-fit max-h-[85vh] w-[min(640px,96vw)] max-w-none overflow-auto rounded-lg border border-border bg-b-1 p-4 text-c-1 [box-shadow:var(--scalar-shadow-2,0_10px_30px_rgb(0_0_0/0.25))] backdrop:bg-[rgb(0_0_0/0.45)] open:flex open:flex-col"
        @click="closeFlowOnBackdrop"
        @keydown.esc.stop>
        <div
          class="ba-dialog-header mb-2 flex items-center justify-between gap-2">
          <div
            class="ba-overview-title text-(length:--scalar-mini) font-bold tracking-[normal] text-c-2">
            What flows · {{ flowEdge?.label }}
          </div>
          <div class="ba-dialog-actions flex items-center gap-1.5">
            <button
              aria-label="Close"
              class="ba-icon-button"
              title="Close"
              type="button"
              @click="flowDialog?.close()">
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
        <ArazzoDataFlowDetails
          v-if="flowDetails"
          v-bind="flowDetails"
          @navigate="closeFlow" />
      </dialog>
    </Teleport>
  </section>
</template>
