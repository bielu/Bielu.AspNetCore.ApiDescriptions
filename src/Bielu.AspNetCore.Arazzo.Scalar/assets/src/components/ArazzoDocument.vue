<script setup lang="ts">
import {
  computed,
  nextTick,
  onBeforeUnmount,
  onErrorCaptured,
  onMounted,
  ref,
  watch,
} from 'vue'
import { renderMarkdown } from '../helpers/markdown'
import { type DocumentWatch, watchDocuments } from '../helpers/document-watch'
import {
  componentsEntryId,
  STEP_REVEAL_EVENT,
  toWorkflowViews,
} from '../helpers/navigation'
import { normalizeArazzoDocument } from '../helpers/normalize'
import { sourcesLoaded } from '../helpers/resolve-target'
import type { ArazzoPluginOptions } from '../helpers/plugin-options'
import { filterWorkflows } from '../helpers/search'
import {
  countStepsBySpec,
  specLabel,
  summarizeSources,
} from '../helpers/target-spec'
import type {
  ArazzoDocument,
  PluginEventBus,
  PluginNavigationEntry,
} from '../types'
import { diagnose } from '../helpers/diagnostics'
import { provideDiagnostics } from '../helpers/diagnostics-scope'
import ArazzoComponents from './ArazzoComponents.vue'
import ArazzoDiagnostics from './ArazzoDiagnostics.vue'
import ArazzoExtensions from './ArazzoExtensions.vue'
import ArazzoWorkflow from './ArazzoWorkflow.vue'

/**
 * The content of an Arazzo document in Scalar. Scalar renders it in place of its own introduction
 * and content tree, and hands it the props below (see `PluginDocumentType.content` in `@scalar/types`).
 */
const props = defineProps<{
  arazzoOptions?: ArazzoPluginOptions
  document?: ArazzoDocument
  /** The workspace document name (the slug of the configured source). */
  documentName?: string
  /** Every loaded workspace document, so step targets resolve into sibling OpenAPI/AsyncAPI documents. */
  documents?: Record<string, unknown>
  /** The sidebar entries `traverse` built for this document. */
  navigation?: PluginNavigationEntry[]
  eventBus?: PluginEventBus
  /** Scalar's configuration; `layout` (`modern` or `classic`) is the part read here. */
  options?: Record<string, unknown>
}>()

const root = ref<HTMLElement>()

const layout = computed(() =>
  typeof props.options?.layout === 'string' ? props.options.layout : 'modern',
)

/**
 * The document as the view reads it: malformed entries dropped, so a broken document renders what it
 * can instead of failing. The same (cached) copy the sidebar was built from.
 */
const arazzo = computed(() => normalizeArazzoDocument(props.document))

const info = computed(() => arazzo.value?.info ?? {})

/**
 * A part of the document that failed to render. The error stops at this boundary, so the rest of the
 * document still shows, with a note saying what is missing, instead of an empty page.
 */
const renderError = ref<string>()
onErrorCaptured((error) => {
  renderError.value = error instanceof Error ? error.message : String(error)
  console.error(
    '[bielu-arazzo] Part of the Arazzo document could not be rendered.',
    error,
  )
  return false
})

const description = computed(() => renderMarkdown(info.value.description))

const workflows = computed(() =>
  arazzo.value ? toWorkflowViews(arazzo.value, props.navigation ?? []) : [],
)

/** The document's own extensions and its `info`'s, shown together in the header (the root's win on a clash). */
const documentExtensions = computed(() => ({ ...info.value, ...arazzo.value }))

/** The text in the filter box; the workflows and steps it leaves are `filter`, everything when empty. */
const query = ref('')
const matches = computed(() =>
  filterWorkflows(
    workflows.value,
    query.value,
    arazzo.value
      ? { document: arazzo.value, config: props.arazzoOptions ?? {} }
      : undefined,
  ),
)

/**
 * Steps opened from an overview while the filter hid them: shown, with their workflow, on top of what the filter
 * leaves, until the query changes. Otherwise a box would open a section nobody can see.
 */
const revealed = ref<ReadonlySet<string>>(new Set())
/** Workflows a link landed in outside their steps (another workflow's output) while the filter hid them. */
const revealedWorkflows = ref<ReadonlySet<string>>(new Set())
watch(query, () => {
  revealed.value = new Set()
  revealedWorkflows.value = new Set()
})
const filter = computed(() => {
  const current = matches.value
  if (
    !current ||
    (revealed.value.size === 0 && revealedWorkflows.value.size === 0)
  ) {
    return current
  }
  const steps = new Set([...current.steps, ...revealed.value])
  const owners = workflows.value
    .filter((view) => view.steps.some((entry) => revealed.value.has(entry.id)))
    .map((view) => view.id)
  return {
    ...current,
    steps,
    workflows: new Set([
      ...current.workflows,
      ...owners,
      ...revealedWorkflows.value,
    ]),
  }
})
const onRevealStep = (event: Event) => {
  const { sectionId, workflowId } =
    (event as CustomEvent<{ sectionId?: string; workflowId?: string }>)
      .detail ?? {}
  if (sectionId && matches.value && !matches.value.steps.has(sectionId)) {
    revealed.value = new Set([...revealed.value, sectionId])
  }
  if (workflowId && matches.value && !matches.value.workflows.has(workflowId)) {
    revealedWorkflows.value = new Set([...revealedWorkflows.value, workflowId])
  }
}
onMounted(() => root.value?.addEventListener(STEP_REVEAL_EVENT, onRevealStep))
onBeforeUnmount(() =>
  root.value?.removeEventListener(STEP_REVEAL_EVENT, onRevealStep),
)

/** workflowId → stepId → section id, so an overview box opens its step in whichever workflow it belongs to. */
const stepSectionIds = computed(() =>
  Object.fromEntries(
    workflows.value.map((view) => [
      view.workflow.workflowId,
      Object.fromEntries(
        view.steps.map((entry) => [entry.step.stepId, entry.id]),
      ),
    ]),
  ),
)

/** workflowId → its section's sidebar id, so a step that invokes a workflow links to it. */
const workflowNavIds = computed(() =>
  Object.fromEntries(
    workflows.value.map((view) => [view.workflow.workflowId, view.id]),
  ),
)

/** The components section's sidebar id; `undefined` when the document declares no components. */
const componentsId = computed(() => componentsEntryId(props.navigation ?? []))

/**
 * What looks wrong in the document: references to things it doesn't declare, parameters and statuses its
 * targets don't take or return, steps that can never run. Re-checked as sibling documents load.
 */
const diagnostics = computed(() =>
  arazzo.value
    ? diagnose(arazzo.value, {
        documents: loadedDocuments.value,
        workflowNavIds: workflowNavIds.value,
        stepSectionIds: stepSectionIds.value,
        componentsId: componentsId.value,
      })
    : [],
)
provideDiagnostics(() => diagnostics.value)

/** Each source description with its spec, whether it is loaded, and how many steps use it. */
const sources = computed(() =>
  arazzo.value ? summarizeSources(arazzo.value, loadedDocuments.value) : [],
)

/** "3 OpenAPI steps · 2 AsyncAPI steps": how the workflows split between the specs. */
const stepsBySpec = computed(() =>
  arazzo.value ? countStepsBySpec(arazzo.value, loadedDocuments.value) : [],
)

/**
 * The workspace documents, re-read whenever one is added. Scalar loads the sibling documents after
 * this one renders and adds them to the same object in place; that object is reactive in Scalar's
 * Vue, not in this element's own Vue, so nothing re-renders on its own. `watchDocuments` compares
 * the document names with a back-off, and stops once every source description is loaded.
 */
const loadedDocuments = ref<Record<string, unknown>>({ ...props.documents })
const documentNames = () => Object.keys(props.documents ?? {}).join('\n')
let lastDocumentNames = documentNames()
const refreshDocuments = (): boolean => {
  const names = documentNames()
  if (names === lastDocumentNames) {
    return false
  }
  lastDocumentNames = names
  loadedDocuments.value = { ...props.documents }
  return true
}

/**
 * Every source description has a loaded document to resolve into, Arazzo ones included, and so do the Arazzo
 * sources of the Arazzo documents they lead to: a workflow called in another document can load after this one.
 */
const allSourcesLoaded = () =>
  arazzo.value ? sourcesLoaded(arazzo.value, props.documents ?? {}) : true
watch(
  () => props.documents,
  () => {
    lastDocumentNames = documentNames()
    loadedDocuments.value = { ...props.documents }
    // A new workspace object (a configuration change) may still be filling up: look again soon.
    documentsWatch?.poke()
  },
)
let documentsWatch: DocumentWatch | undefined
const onVisibilityChange = () => documentsWatch?.poke()

/**
 * Keeps the sidebar and URL in sync while scrolling, which Scalar leaves to the plugin: the active
 * entry is the last section (in document order) inside the top band of the viewport. Steps come
 * after their workflow's heading, so a visible step wins over the workflow that contains it.
 */
let observer: IntersectionObserver | undefined
const visible = new Set<Element>()

const observeSections = () => {
  observer?.disconnect()
  visible.clear()
  if (!root.value || typeof IntersectionObserver === 'undefined') {
    return
  }
  const sections = Array.from(
    root.value.querySelectorAll<HTMLElement>('[data-nav-id]'),
  )
  observer = new IntersectionObserver(
    (records) => {
      for (const record of records) {
        if (record.isIntersecting) {
          visible.add(record.target)
        } else {
          visible.delete(record.target)
        }
      }
      const active = sections.filter((section) => visible.has(section)).pop()
      if (active?.dataset.navId) {
        props.eventBus?.emit('intersecting:nav-item', {
          id: active.dataset.navId,
        })
      }
    },
    { rootMargin: '0px 0px -70% 0px' },
  )
  sections.forEach((section) => observer!.observe(section))
}

onMounted(() => {
  observeSections()
  documentsWatch = watchDocuments({
    check: refreshDocuments,
    isComplete: allSourcesLoaded,
    isHidden: () =>
      typeof document !== 'undefined' && document.visibilityState === 'hidden',
  })
  document.addEventListener('visibilitychange', onVisibilityChange)
})
watch(workflows, () => nextTick(observeSections))
onBeforeUnmount(() => {
  observer?.disconnect()
  documentsWatch?.stop()
  document.removeEventListener('visibilitychange', onVisibilityChange)
})
</script>

<template>
  <article
    ref="root"
    class="bielu-arazzo">
    <header class="ba-header">
      <div class="ba-kicker">
        Arazzo {{ arazzo?.arazzo }}
        <template v-if="info.version">· v{{ info.version }}</template>
      </div>
      <h1 class="ba-title">{{ info.title || documentName }}</h1>
      <p
        v-if="renderError"
        class="ba-render-error"
        role="alert">
        Part of this document could not be shown: {{ renderError }}
      </p>
      <div
        v-if="arazzo?.$self"
        class="ba-self ba-muted">
        Canonical URL
        <a
          :href="arazzo.$self"
          rel="noopener"
          target="_blank">
          {{ arazzo.$self }}
        </a>
      </div>
      <p
        v-if="info.summary"
        class="ba-summary">
        {{ info.summary }}
      </p>
      <div
        v-if="description"
        class="ba-markdown"
        v-html="description" />
      <ArazzoExtensions :of="documentExtensions" />

      <div
        v-if="arazzo?.sourceDescriptions?.length"
        class="ba-block">
        <div class="ba-block-title ba-sources-title">
          Sources
          <span
            v-if="stepsBySpec.length"
            class="ba-spec-counts">
            <span
              v-for="entry in stepsBySpec"
              :key="entry.family"
              class="ba-spec"
              :class="`ba-spec-${entry.family}`">
              {{ entry.count }}
              {{
                entry.family === 'arazzo'
                  ? 'workflow'
                  : specLabel({ family: entry.family, version: undefined })
              }}
              {{ entry.count === 1 ? 'step' : 'steps' }}
            </span>
          </span>
        </div>
        <table class="ba-table ba-sources">
          <tbody>
            <tr
              v-for="summary in sources"
              :key="summary.source.name">
              <td>
                <code>{{ summary.source.name }}</code>
              </td>
              <td>
                <span
                  v-if="summary.family"
                  class="ba-spec"
                  :class="[
                    `ba-spec-${summary.family}`,
                    { 'ba-spec-unresolved': !summary.documentName },
                  ]">
                  {{
                    specLabel({
                      family: summary.family,
                      version: summary.version,
                    })
                  }}
                </span>
                <span
                  v-else
                  class="ba-muted">
                  {{ summary.source.type ?? 'unknown' }}
                </span>
              </td>
              <td class="ba-muted ba-nowrap">
                <template v-if="!summary.documentName">Not loaded · </template>
                {{ summary.stepCount }}
                {{ summary.stepCount === 1 ? 'step' : 'steps' }}
              </td>
              <td>
                <a
                  :href="summary.source.url"
                  rel="noopener"
                  target="_blank">
                  {{ summary.source.url }}
                </a>
                <ArazzoExtensions :of="summary.source" />
              </td>
            </tr>
          </tbody>
        </table>
      </div>
      <ArazzoDiagnostics :diagnostics="diagnostics" />
      <div
        v-if="workflows.length"
        class="ba-filter"
        role="search">
        <input
          v-model="query"
          aria-label="Filter workflows and steps"
          class="ba-filter-input"
          placeholder="Filter workflows and steps"
          type="search" />
        <span
          aria-live="polite"
          class="ba-muted">
          <template v-if="filter"
            >{{ filter.matchingSteps }} of
            {{ filter.totalSteps }} steps</template
          >
        </span>
      </div>
    </header>

    <p
      v-if="filter && filter.workflows.size === 0"
      class="ba-muted">
      No workflow or step matches “{{ query }}”.
    </p>

    <ArazzoWorkflow
      :arazzoOptions="arazzoOptions"
      v-for="view in workflows"
      v-show="!filter || filter.workflows.has(view.id)"
      :key="view.id"
      :stepSectionIds="stepSectionIds"
      :componentsId="componentsId"
      :documentName="documentName"
      :visibleSteps="filter?.steps"
      :document="arazzo!"
      :documents="loadedDocuments"
      :eventBus="eventBus"
      :layout="layout"
      :view="view"
      :workflowNavIds="workflowNavIds" />

    <ArazzoComponents
      v-if="arazzo?.components && componentsId"
      :id="componentsId"
      :components="arazzo.components"
      :document="arazzo"
      :eventBus="eventBus"
      :workflowNavIds="workflowNavIds" />

    <p
      v-if="!workflows.length"
      class="ba-muted">
      This document declares no workflows.
    </p>
  </article>
</template>

<!-- The styles for every Arazzo component, in src/styles/ (see ../style.css for why they live there). -->
<style src="../style.css"></style>
