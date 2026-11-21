<script setup lang="ts">
import { computed, nextTick, onBeforeUnmount, onErrorCaptured, onMounted, ref, watch } from 'vue'
import { renderMarkdown } from '../helpers/markdown'
import { type DocumentWatch, watchDocuments } from '../helpers/document-watch'
import { componentsEntryId, STEP_REVEAL_EVENT, toWorkflowViews } from '../helpers/navigation'
import { normalizeArazzoDocument } from '../helpers/normalize'
import { sourcesLoaded } from '../helpers/resolve-target'
import type { ArazzoPluginOptions } from '../helpers/plugin-options'
import { filterWorkflows } from '../helpers/search'
import { countStepsBySpec, specLabel, summarizeSources } from '../helpers/target-spec'
import type { ArazzoDocument, PluginEventBus, PluginNavigationEntry } from '../types'
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

const layout = computed(() => (typeof props.options?.layout === 'string' ? props.options.layout : 'modern'))

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
  console.error('[bielu-arazzo] Part of the Arazzo document could not be rendered.', error)
  return false
})

const description = computed(() => renderMarkdown(info.value.description))

const workflows = computed(() => (arazzo.value ? toWorkflowViews(arazzo.value, props.navigation ?? []) : []))

/** The document's own extensions and its `info`'s, shown together in the header (the root's win on a clash). */
const documentExtensions = computed(() => ({ ...info.value, ...arazzo.value }))

/** The text in the filter box; the workflows and steps it leaves are `filter`, everything when empty. */
const query = ref('')
const matches = computed(() =>
  filterWorkflows(workflows.value, query.value, arazzo.value ? { document: arazzo.value, config: props.arazzoOptions ?? {} } : undefined),
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
  if (!current || (revealed.value.size === 0 && revealedWorkflows.value.size === 0)) {
    return current
  }
  const steps = new Set([...current.steps, ...revealed.value])
  const owners = workflows.value.filter((view) => view.steps.some((entry) => revealed.value.has(entry.id))).map((view) => view.id)
  return { ...current, steps, workflows: new Set([...current.workflows, ...owners, ...revealedWorkflows.value]) }
})
const onRevealStep = (event: Event) => {
  const { sectionId, workflowId } = (event as CustomEvent<{ sectionId?: string; workflowId?: string }>).detail ?? {}
  if (sectionId && matches.value && !matches.value.steps.has(sectionId)) {
    revealed.value = new Set([...revealed.value, sectionId])
  }
  if (workflowId && matches.value && !matches.value.workflows.has(workflowId)) {
    revealedWorkflows.value = new Set([...revealedWorkflows.value, workflowId])
  }
}
onMounted(() => root.value?.addEventListener(STEP_REVEAL_EVENT, onRevealStep))
onBeforeUnmount(() => root.value?.removeEventListener(STEP_REVEAL_EVENT, onRevealStep))

/** workflowId → stepId → section id, so an overview box opens its step in whichever workflow it belongs to. */
const stepSectionIds = computed(() =>
  Object.fromEntries(workflows.value.map((view) => [view.workflow.workflowId, Object.fromEntries(view.steps.map((entry) => [entry.step.stepId, entry.id]))])),
)

/** workflowId → its section's sidebar id, so a step that invokes a workflow links to it. */
const workflowNavIds = computed(() => Object.fromEntries(workflows.value.map((view) => [view.workflow.workflowId, view.id])))

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
const sources = computed(() => (arazzo.value ? summarizeSources(arazzo.value, loadedDocuments.value) : []))

/** "3 OpenAPI steps · 2 AsyncAPI steps": how the workflows split between the specs. */
const stepsBySpec = computed(() => (arazzo.value ? countStepsBySpec(arazzo.value, loadedDocuments.value) : []))

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
const allSourcesLoaded = () => (arazzo.value ? sourcesLoaded(arazzo.value, props.documents ?? {}) : true)
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
  const sections = Array.from(root.value.querySelectorAll<HTMLElement>('[data-nav-id]'))
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
        props.eventBus?.emit('intersecting:nav-item', { id: active.dataset.navId })
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
    isHidden: () => typeof document !== 'undefined' && document.visibilityState === 'hidden',
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
              {{ entry.count }} {{ entry.family === 'arazzo' ? 'workflow' : specLabel({ family: entry.family, version: undefined }) }}
              {{ entry.count === 1 ? 'step' : 'steps' }}
            </span>
          </span>
        </div>
        <table class="ba-table ba-sources">
          <tbody>
            <tr
              v-for="summary in sources"
              :key="summary.source.name">
              <td><code>{{ summary.source.name }}</code></td>
              <td>
                <span
                  v-if="summary.family"
                  class="ba-spec"
                  :class="[`ba-spec-${summary.family}`, { 'ba-spec-unresolved': !summary.documentName }]">
                  {{ specLabel({ family: summary.family, version: summary.version }) }}
                </span>
                <span
                  v-else
                  class="ba-muted">
                  {{ summary.source.type ?? 'unknown' }}
                </span>
              </td>
              <td class="ba-muted ba-nowrap">
                <template v-if="!summary.documentName">Not loaded · </template>
                {{ summary.stepCount }} {{ summary.stepCount === 1 ? 'step' : 'steps' }}
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
          <template v-if="filter">{{ filter.matchingSteps }} of {{ filter.totalSteps }} steps</template>
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

<style>
/*
 * Unscoped but prefixed: the element renders in the light DOM (shadowRoot: false) so Scalar's design
 * tokens apply, and only the root component's styles are injected, so the child components' styles
 * live here too. Every rule sits under `.bielu-arazzo`, so nothing leaks into Scalar's page.
 */
.bielu-arazzo {
  display: block;
  max-width: 1360px;
  margin: 0 auto;
  padding: 48px 60px 96px;
  color: var(--scalar-color-1);
  font-family: var(--scalar-font);
  font-size: var(--scalar-paragraph, 16px);
  line-height: 1.5;
  /* The workflow layout switches to two columns on the element's own width, not the viewport's. */
  container: bielu-arazzo / inline-size;
}
@media (max-width: 1000px) {
  .bielu-arazzo {
    padding: 24px 16px 64px;
  }
}
.bielu-arazzo code,
.bielu-arazzo pre {
  font-family: var(--scalar-font-code);
  font-size: var(--scalar-small, 13px);
}
.bielu-arazzo a {
  color: var(--scalar-color-accent);
  word-break: break-all;
}
.bielu-arazzo .ba-kicker {
  font-size: var(--scalar-mini, 12px);
  font-weight: var(--scalar-semibold, 600);
  color: var(--scalar-color-2);
  letter-spacing: normal;
}
.bielu-arazzo .ba-title {
  margin: 4px 0 8px;
  font-size: var(--scalar-heading-1, 32px);
  font-weight: var(--scalar-bold, 700);
}
.bielu-arazzo .ba-summary {
  margin: 0 0 8px;
  color: var(--scalar-color-2);
}
.bielu-arazzo .ba-muted {
  color: var(--scalar-color-3);
  font-size: var(--scalar-small, 13px);
}
.bielu-arazzo .ba-markdown {
  color: var(--scalar-color-2);
}
.bielu-arazzo .ba-markdown :first-child {
  margin-top: 0;
}
.bielu-arazzo .ba-markdown :last-child {
  margin-bottom: 0;
}
.bielu-arazzo .ba-workflow {
  margin-top: 48px;
  padding-top: 32px;
  border-top: var(--scalar-border-width, 1px) solid var(--scalar-border-color);
  scroll-margin-top: var(--refs-viewport-offset, 0px);
}
.bielu-arazzo .ba-workflow-title {
  margin: 4px 0 8px;
  font-size: var(--scalar-heading-2, 24px);
  font-weight: var(--scalar-bold, 700);
}
.bielu-arazzo .ba-steps {
  list-style: none;
  margin: 24px 0 0;
  padding: 0;
}
.bielu-arazzo .ba-steps > li {
  position: relative;
  padding-left: 40px;
}
/* The number comes from the step's position, not a CSS counter, so a filter that hides steps does not renumber the rest. */
.bielu-arazzo .ba-steps > li::before {
  content: attr(data-step-number);
  position: absolute;
  left: 0;
  top: 18px;
  width: 24px;
  height: 24px;
  border-radius: 50%;
  background: var(--scalar-background-2);
  border: var(--scalar-border-width, 1px) solid var(--scalar-border-color);
  color: var(--scalar-color-2);
  font-size: var(--scalar-mini, 12px);
  line-height: 22px;
  text-align: center;
}
.bielu-arazzo .ba-steps > li:not(:last-child)::after {
  content: '';
  position: absolute;
  left: 12px;
  top: 46px;
  bottom: -16px;
  border-left: var(--scalar-border-width, 1px) solid var(--scalar-border-color);
}
.bielu-arazzo .ba-step {
  padding: 16px 0;
  scroll-margin-top: var(--refs-viewport-offset, 0px);
}
.bielu-arazzo .ba-step-title {
  margin: 2px 0 8px;
  font-size: var(--scalar-heading-4, 18px);
  font-weight: var(--scalar-semibold, 600);
}
.bielu-arazzo .ba-badges {
  display: flex;
  flex-wrap: wrap;
  gap: 6px;
  margin: 8px 0;
}
.bielu-arazzo .ba-badge {
  display: inline-block;
  padding: 1px 8px;
  border-radius: var(--scalar-radius, 4px);
  background: var(--scalar-background-2);
  border: var(--scalar-border-width, 1px) solid var(--scalar-border-color);
  color: var(--scalar-color-2);
  font-size: var(--scalar-mini, 12px);
}
/*
 * Spec markers: one color per spec, shared by the step badge, the header summary and the overview
 * diagram, so a mixed OpenAPI/AsyncAPI workflow reads at a glance. The text always names the spec
 * too, so the color is never the only cue.
 */
.bielu-arazzo {
  --ba-spec-openapi: var(--scalar-color-blue, #2f6fdb);
  --ba-spec-asyncapi: var(--scalar-color-purple, #8a4fd8);
  --ba-spec-arazzo: var(--scalar-color-3, #888);
}
.bielu-arazzo .ba-spec {
  --ba-spec-color: var(--scalar-color-3);
  display: inline-block;
  margin-right: 8px;
  padding: 0 7px;
  border: var(--scalar-border-width, 1px) solid transparent;
  border-radius: var(--scalar-radius, 4px);
  background: color-mix(in srgb, var(--ba-spec-color) 9%, transparent);
  color: var(--scalar-color-2);
  font-size: var(--scalar-mini, 12px);
  font-weight: var(--scalar-semibold, 600);
  white-space: nowrap;
}
.bielu-arazzo .ba-spec-openapi {
  --ba-spec-color: var(--ba-spec-openapi);
}
.bielu-arazzo .ba-spec-asyncapi {
  --ba-spec-color: var(--ba-spec-asyncapi);
}
.bielu-arazzo .ba-spec-arazzo {
  --ba-spec-color: var(--ba-spec-arazzo);
}
.bielu-arazzo .ba-spec-unresolved {
  border-color: var(--scalar-border-color);
  border-style: dashed;
  background: none;
}
.bielu-arazzo .ba-sources-title {
  display: flex;
  flex-wrap: wrap;
  align-items: center;
  justify-content: space-between;
  gap: 8px;
}
.bielu-arazzo .ba-spec-counts .ba-spec {
  margin: 0 0 0 6px;
  font-weight: normal;
  background: none;
  padding: 0;
  color: var(--scalar-color-3);
}
.bielu-arazzo .ba-nowrap {
  white-space: nowrap;
}
.bielu-arazzo .ba-target {
  margin: 16px 0;
  padding: 0;
  min-width: 0;
}
.bielu-arazzo .ba-target-header {
  display: flex;
  flex-wrap: wrap;
  align-items: center;
  gap: 6px;
  padding: 10px 12px;
  border: var(--scalar-border-width, 1px) solid var(--scalar-border-color);
  border-radius: var(--scalar-radius, 4px);
  background: var(--scalar-background-2);
}
.bielu-arazzo .ba-target-meta {
  display: flex;
  flex-wrap: wrap;
  gap: 6px 16px;
  margin: 8px 0 12px;
  color: var(--scalar-color-3);
  font-size: var(--scalar-small, 13px);
  overflow-wrap: anywhere;
}
.bielu-arazzo .ba-target-unresolved .ba-target-header {
  border-style: dashed;
}
.bielu-arazzo .ba-target-label {
  margin-right: 8px;
  font-size: var(--scalar-mini, 12px);
  font-weight: var(--scalar-semibold, 600);
  color: var(--scalar-color-2);
  text-transform: uppercase;
}
.bielu-arazzo .ba-target-id {
  margin-right: 8px;
  word-break: break-all;
}
.bielu-arazzo a.ba-target-link {
  display: inline-flex;
  flex-wrap: wrap;
  align-items: baseline;
  gap: 0 8px;
  color: inherit;
  text-decoration: none;
  word-break: normal;
}
.bielu-arazzo a.ba-target-link .ba-target-id,
.bielu-arazzo a.ba-target-link .ba-target-signature {
  margin-right: 0;
}
.bielu-arazzo a.ba-target-link:hover .ba-target-id,
.bielu-arazzo a.ba-target-link:hover .ba-target-signature,
.bielu-arazzo a.ba-target-link:focus-visible .ba-target-id {
  color: var(--scalar-color-accent);
  text-decoration: underline;
}
.bielu-arazzo .ba-target-arrow {
  color: var(--scalar-color-accent);
  font-size: var(--scalar-small, 13px);
}
.bielu-arazzo .ba-target-signature {
  margin-right: 8px;
  font-family: var(--scalar-font-code);
  font-weight: var(--scalar-semibold, 600);
}
.bielu-arazzo .ba-target-summary {
  margin-top: 4px;
  color: var(--scalar-color-2);
}
.bielu-arazzo .ba-block {
  margin-top: 16px;
}
.bielu-arazzo .ba-block-title {
  margin-bottom: 8px;
  padding-bottom: 6px;
  border-bottom: var(--scalar-border-width, 1px) solid var(--scalar-border-color);
  font-size: var(--scalar-small, 13px);
  font-weight: var(--scalar-semibold, 600);
}
.bielu-arazzo .ba-table {
  width: 100%;
  border-collapse: collapse;
}
.bielu-arazzo .ba-table td {
  padding: 6px 8px 6px 0;
  vertical-align: top;
  border-bottom: var(--scalar-border-width, 1px) solid var(--scalar-border-color);
}
.bielu-arazzo .ba-required {
  margin-left: 6px;
  color: var(--scalar-color-orange, orange);
  font-size: var(--scalar-mini, 12px);
}
.bielu-arazzo .ba-key-values {
  display: grid;
  grid-template-columns: minmax(120px, auto) 1fr;
  gap: 4px 16px;
  margin: 0;
}
.bielu-arazzo .ba-key-values dt {
  font-weight: var(--scalar-semibold, 600);
}
.bielu-arazzo .ba-key-values dd {
  margin: 0;
  color: var(--scalar-color-2);
  word-break: break-all;
  white-space: pre-wrap;
}
.bielu-arazzo .ba-criteria,
.bielu-arazzo .ba-actions {
  margin: 0;
  padding-left: 20px;
}
.bielu-arazzo .ba-actions li {
  display: flex;
  flex-wrap: wrap;
  align-items: baseline;
  gap: 8px;
  margin-bottom: 4px;
}
.bielu-arazzo .ba-action-name {
  font-weight: var(--scalar-semibold, 600);
}
.bielu-arazzo .ba-condition {
  color: var(--scalar-color-2);
}
.bielu-arazzo .ba-criteria li {
  margin-bottom: 2px;
}
.bielu-arazzo .ba-criteria .ba-condition {
  color: var(--scalar-color-1);
}
.bielu-arazzo .ba-criteria-inline {
  display: inline-flex;
  flex-wrap: wrap;
  gap: 4px 10px;
  padding: 0;
  list-style: none;
}
.bielu-arazzo .ba-criteria-inline .ba-condition {
  color: var(--scalar-color-2);
}
.bielu-arazzo .ba-target-message,
.bielu-arazzo .ba-target-body,
.bielu-arazzo .ba-target-responses {
  margin-top: 24px;
}
/* An OpenAPI target's responses: one line per status, its schema folding out beneath. */
.bielu-arazzo .ba-responses {
  margin: 0;
  padding: 0;
  list-style: none;
}
.bielu-arazzo .ba-responses > li {
  padding: 6px 0;
  border-bottom: var(--scalar-border-width, 1px) solid var(--scalar-border-color);
}
.bielu-arazzo .ba-response > summary,
.bielu-arazzo .ba-response-empty {
  display: flex;
  flex-wrap: wrap;
  align-items: baseline;
  gap: 4px 8px;
}
.bielu-arazzo .ba-response > summary {
  cursor: pointer;
}
.bielu-arazzo .ba-response[open] > summary {
  margin-bottom: 6px;
}
.bielu-arazzo .ba-response-description {
  color: var(--scalar-color-2);
}
.bielu-arazzo .ba-status {
  font-weight: var(--scalar-semibold, 600);
}
.bielu-arazzo .ba-status-2 {
  color: var(--scalar-color-green, #30a46c);
}
.bielu-arazzo .ba-status-3 {
  color: var(--scalar-color-blue, #0081f1);
}
.bielu-arazzo .ba-status-4 {
  color: var(--scalar-color-orange, #f76b15);
}
.bielu-arazzo .ba-status-5 {
  color: var(--scalar-color-red, #e5484d);
}
.bielu-arazzo .ba-response-handled {
  border-color: var(--scalar-color-green, #30a46c);
}
.bielu-arazzo .ba-target-message .ba-block-title code {
  margin: 0 4px;
}
.bielu-arazzo .ba-subtitle {
  margin: 10px 0 4px;
  color: var(--scalar-color-2);
  font-size: var(--scalar-mini, 12px);
  font-weight: var(--scalar-semibold, 600);
}
.bielu-arazzo .ba-replacements td:nth-child(2) {
  width: 1.5em;
  text-align: center;
}
/* Diagnostics: the header's folding list, and each step's own problems. */
.bielu-arazzo .ba-diagnostics {
  margin: 16px 0;
  padding: 8px 12px;
  border: var(--scalar-border-width, 1px) solid var(--ba-diagnostic-color);
  border-radius: var(--scalar-radius-lg, 8px);
  background: color-mix(in srgb, var(--ba-diagnostic-color) 8%, transparent);
}
.bielu-arazzo .ba-diagnostics-error,
.bielu-arazzo .ba-diagnostic-error {
  --ba-diagnostic-color: var(--scalar-color-red, #e5484d);
}
.bielu-arazzo .ba-diagnostics-warning,
.bielu-arazzo .ba-diagnostic-warning {
  --ba-diagnostic-color: var(--scalar-color-orange, #f76b15);
}
.bielu-arazzo .ba-diagnostics > summary {
  cursor: pointer;
  font-size: var(--scalar-small, 13px);
  font-weight: var(--scalar-semibold, 600);
}
.bielu-arazzo .ba-diagnostic-icon {
  display: inline-block;
  width: 16px;
  height: 16px;
  margin-right: 4px;
  border-radius: 50%;
  background: var(--ba-diagnostic-color);
  color: var(--scalar-background-1);
  font-size: 11px;
  line-height: 16px;
  text-align: center;
}
.bielu-arazzo .ba-diagnostic-list,
.bielu-arazzo .ba-step-diagnostics {
  margin: 8px 0 0;
  padding: 0;
  list-style: none;
  font-size: var(--scalar-small, 13px);
}
.bielu-arazzo .ba-diagnostic-list li,
.bielu-arazzo .ba-step-diagnostics li {
  display: flex;
  flex-wrap: wrap;
  gap: 2px 8px;
  padding: 3px 0 3px 8px;
  border-left: 3px solid var(--ba-diagnostic-color);
  margin-bottom: 4px;
}
.bielu-arazzo .ba-diagnostic-severity {
  color: var(--ba-diagnostic-color);
  font-weight: var(--scalar-semibold, 600);
  text-transform: uppercase;
  font-size: var(--scalar-mini, 12px);
}
.bielu-arazzo .ba-diagnostic-where {
  font-family: var(--scalar-font-code);
}
.bielu-arazzo .ba-step-problems {
  margin-left: 6px;
  padding: 0 6px;
  border-radius: 9px;
  background: var(--ba-diagnostic-color);
  color: var(--scalar-background-1);
  font-size: var(--scalar-mini, 12px);
  font-weight: var(--scalar-semibold, 600);
  vertical-align: middle;
}
.bielu-arazzo .ba-diagram-problem circle {
  fill: var(--ba-diagnostic-color);
  stroke: var(--scalar-background-1);
  stroke-width: 1.5;
}
.bielu-arazzo .ba-diagram-problem text {
  fill: var(--scalar-background-1);
  font-family: var(--scalar-font);
  font-size: 10px;
  font-weight: 700;
}
/* Runtime Expressions: a link to what they read, or marked when they read something the document does not declare. */
.bielu-arazzo .ba-expression {
  color: var(--scalar-color-1);
}
.bielu-arazzo a.ba-expression-link {
  color: var(--scalar-color-accent);
  text-decoration: underline dotted;
  text-underline-offset: 2px;
  word-break: inherit;
}
.bielu-arazzo a.ba-expression-link:hover {
  text-decoration-style: solid;
}
.bielu-arazzo .ba-expression-broken {
  color: var(--scalar-color-red, #e5484d);
  text-decoration: underline wavy;
  text-decoration-thickness: 1px;
  text-underline-offset: 3px;
  cursor: help;
}
.bielu-arazzo .ba-used-by {
  display: block;
  color: var(--scalar-color-3);
  font-size: var(--scalar-mini, 12px);
  white-space: normal;
}
.bielu-arazzo .ba-anchor-target {
  scroll-margin-top: var(--refs-viewport-offset, 0px);
  border-radius: var(--scalar-radius, 4px);
  transition: background-color 0.4s;
}
.bielu-arazzo .ba-flash {
  background: color-mix(in srgb, var(--scalar-color-accent) 22%, transparent);
}
.bielu-arazzo .ba-selector {
  display: inline-flex;
  flex-wrap: wrap;
  align-items: baseline;
  gap: 0 4px;
  white-space: normal;
}
/*
 * Phone width: a table row's cells wrap onto more lines instead of squeezing each column down to a
 * few characters (a source URL broken mid-word, a parameter description one word per line).
 */
@container bielu-arazzo (max-width: 560px) {
  .bielu-arazzo .ba-table,
  .bielu-arazzo .ba-table tbody {
    display: block;
  }
  .bielu-arazzo .ba-table tr {
    display: flex;
    flex-wrap: wrap;
    align-items: baseline;
    gap: 2px 10px;
    padding: 6px 0;
    border-bottom: var(--scalar-border-width, 1px) solid var(--scalar-border-color);
  }
  .bielu-arazzo .ba-table td {
    padding: 0;
    border-bottom: none;
  }
  .bielu-arazzo .ba-table td:last-child:not(:first-child) {
    flex-basis: 100%;
  }
  .bielu-arazzo .ba-replacements td:last-child {
    flex-basis: auto;
  }
  .bielu-arazzo .ba-key-values {
    grid-template-columns: 1fr;
  }
  .bielu-arazzo .ba-key-values dd {
    margin-bottom: 4px;
  }
}
.bielu-arazzo .ba-filter {
  display: flex;
  flex-wrap: wrap;
  align-items: center;
  gap: 6px 12px;
  margin-top: 20px;
}
.bielu-arazzo .ba-filter-input {
  flex: 1 1 240px;
  max-width: 420px;
  padding: 6px 10px;
  border: var(--scalar-border-width, 1px) solid var(--scalar-border-color);
  border-radius: var(--scalar-radius, 4px);
  background: var(--scalar-background-1);
  color: var(--scalar-color-1);
  font: inherit;
  font-size: var(--scalar-small, 13px);
}
.bielu-arazzo .ba-filter-input:focus-visible {
  outline: 2px solid var(--scalar-color-accent);
  outline-offset: 1px;
}
.bielu-arazzo .ba-render-error {
  margin: 8px 0;
  padding: 8px 12px;
  border: var(--scalar-border-width, 1px) solid var(--scalar-color-red, #d4413b);
  border-radius: var(--scalar-radius, 4px);
  background: color-mix(in srgb, var(--scalar-color-red, #d4413b) 10%, transparent);
  font-size: var(--scalar-small, 13px);
}
.bielu-arazzo .ba-diagram:focus-visible {
  outline: 2px solid var(--scalar-color-accent);
  outline-offset: 2px;
}
.bielu-arazzo .ba-diagram-node:focus {
  outline: none;
}
.bielu-arazzo .ba-diagram-node:focus-visible rect {
  stroke: var(--scalar-color-accent);
  stroke-width: 2.5;
}
.bielu-arazzo .ba-self {
  margin: -4px 0 8px;
  font-size: var(--scalar-small, 13px);
}
.bielu-arazzo .ba-extensions {
  margin: 8px 0;
  font-size: var(--scalar-small, 13px);
}
.bielu-arazzo .ba-extensions summary {
  width: fit-content;
  color: var(--scalar-color-2);
  cursor: pointer;
}
.bielu-arazzo .ba-extensions summary:hover {
  color: var(--scalar-color-1);
}
.bielu-arazzo .ba-extensions .ba-key-values {
  margin-top: 4px;
}
.bielu-arazzo .ba-actions .ba-extensions {
  flex-basis: 100%;
  margin: 0 0 4px 12px;
}
.bielu-arazzo .ba-components {
  margin-top: 48px;
  padding-top: 32px;
  border-top: var(--scalar-border-width, 1px) solid var(--scalar-border-color);
}
.bielu-arazzo .ba-component {
  margin-bottom: 12px;
}
.bielu-arazzo .ba-component-name {
  display: inline-block;
  margin-bottom: 4px;
  font-weight: var(--scalar-semibold, 600);
}
.bielu-arazzo .ba-criterion-type {
  margin-right: 6px;
  font-family: var(--scalar-font-code);
}
.bielu-arazzo .ba-action-parameters {
  display: grid;
  grid-template-columns: auto 1fr;
  gap: 2px 12px;
  flex-basis: 100%;
  margin: 2px 0 4px 12px;
  color: var(--scalar-color-2);
  font-size: var(--scalar-small, 13px);
}
.bielu-arazzo .ba-action-parameters dd {
  margin: 0;
  word-break: break-all;
}
.bielu-arazzo .ba-badge code {
  font-size: inherit;
}
.bielu-arazzo .ba-code {
  margin: 0;
  padding: 10px 12px;
  overflow: auto;
  border-radius: var(--scalar-radius, 4px);
  background: var(--scalar-background-2);
}
.bielu-arazzo .ba-step-title-id {
  font-family: var(--scalar-font-code);
}
.bielu-arazzo .ba-anchor {
  display: flex;
  align-items: center;
  gap: 6px;
}
.bielu-arazzo .ba-anchor-button {
  padding: 0 4px;
  border: none;
  background: none;
  color: var(--scalar-color-3);
  font-size: var(--scalar-small, 13px);
  cursor: pointer;
  opacity: 0;
}
.bielu-arazzo .ba-anchor:hover .ba-anchor-button,
.bielu-arazzo .ba-anchor-button:focus-visible {
  opacity: 1;
}
.bielu-arazzo .ba-anchor-button:hover {
  color: var(--scalar-color-accent);
}
.bielu-arazzo .ba-workflow-header {
  display: flex;
  align-items: flex-start;
  justify-content: space-between;
  gap: 16px;
}
.bielu-arazzo .ba-workflow-heading {
  min-width: 0;
}
.bielu-arazzo .ba-workflow-actions {
  display: flex;
  flex-wrap: wrap;
  gap: 6px;
  justify-content: flex-end;
}
.bielu-arazzo .ba-step-header {
  display: flex;
  align-items: flex-start;
  gap: 6px;
}
.bielu-arazzo .ba-step-heading {
  min-width: 0;
  flex: 1;
}
.bielu-arazzo .ba-step-toggle {
  flex-shrink: 0;
  width: 24px;
  height: 24px;
  margin-top: 2px;
  padding: 4px;
  border: none;
  border-radius: var(--scalar-radius, 4px);
  background: none;
  color: var(--scalar-color-3);
  cursor: pointer;
}
.bielu-arazzo .ba-step-toggle:hover,
.bielu-arazzo .ba-step-toggle:focus-visible {
  color: var(--scalar-color-1);
  background: var(--scalar-background-2);
}
.bielu-arazzo .ba-step-toggle:focus-visible {
  outline: 2px solid var(--scalar-color-accent);
  outline-offset: 1px;
}
.bielu-arazzo .ba-chevron {
  display: block;
  width: 16px;
  height: 16px;
  fill: none;
  stroke: currentColor;
  stroke-width: 1.8;
  stroke-linecap: round;
  stroke-linejoin: round;
  transform: rotate(90deg);
  transition: transform 0.15s ease;
}
.bielu-arazzo .ba-step-closed .ba-chevron {
  transform: none;
}
.bielu-arazzo .ba-step-collapsed-summary {
  margin-top: 4px;
  color: var(--scalar-color-2);
  font-size: var(--scalar-small, 13px);
  overflow-wrap: anywhere;
}
@media (prefers-reduced-motion: reduce) {
  .bielu-arazzo .ba-chevron {
    transition: none;
  }
}
.bielu-arazzo .ba-toggle {
  flex-shrink: 0;
  padding: 4px 10px;
  border: var(--scalar-border-width, 1px) solid var(--scalar-border-color);
  border-radius: var(--scalar-radius, 4px);
  background: var(--scalar-background-1);
  color: var(--scalar-color-2);
  font: inherit;
  font-size: var(--scalar-mini, 12px);
  cursor: pointer;
}
.bielu-arazzo .ba-toggle:hover {
  color: var(--scalar-color-1);
  background: var(--scalar-background-2);
}
.bielu-arazzo .ba-icon-button {
  display: inline-flex;
  align-items: center;
  justify-content: center;
  width: 26px;
  height: 26px;
  padding: 0;
  border: none;
  border-radius: var(--scalar-radius, 4px);
  background: none;
  color: var(--scalar-color-2);
  cursor: pointer;
}
.bielu-arazzo .ba-icon-button:hover {
  background: var(--scalar-background-2);
  color: var(--scalar-color-1);
}
/* Steps on the left and the overview beside them once there is room for both, as in the POC. */
.bielu-arazzo .ba-workflow-content {
  display: grid;
  grid-template-columns: minmax(0, 1fr);
  align-items: start;
  gap: 32px;
}
@container bielu-arazzo (min-width: 960px) {
  .bielu-arazzo .ba-workflow-content {
    grid-template-columns: minmax(0, 3fr) minmax(0, 2fr);
  }
  .bielu-arazzo .ba-overview {
    position: sticky;
    top: calc(var(--refs-viewport-offset, 0px) + 24px);
  }
}
.bielu-arazzo .ba-workflow-aside {
  min-width: 0;
  margin-top: 16px;
}
.bielu-arazzo .ba-overview {
  padding: 12px;
  border: var(--scalar-border-width, 1px) solid var(--scalar-border-color);
  border-radius: var(--scalar-radius-lg, 8px);
  background: var(--scalar-background-1);
}
.bielu-arazzo .ba-overview-header,
.bielu-arazzo .ba-dialog-header {
  display: flex;
  align-items: center;
  justify-content: space-between;
  gap: 8px;
  margin-bottom: 8px;
}
.bielu-arazzo .ba-overview-title {
  font-size: var(--scalar-mini, 12px);
  font-weight: var(--scalar-bold, 700);
  letter-spacing: normal;
  color: var(--scalar-color-2);
}
.bielu-arazzo .ba-overview-hint {
  margin-top: 6px;
  font-size: var(--scalar-mini, 12px);
}
.bielu-arazzo .ba-diagram {
  display: flex;
  justify-content: center;
  max-height: min(560px, calc(var(--refs-viewport-height, 100vh) - 140px));
  overflow: hidden;
  cursor: grab;
  touch-action: none;
  user-select: none;
}
/* With a workflow expanded, the inline overview may grow to the height of the window (then it pans), so the
   expanded steps are not cut off at the usual height. */
.bielu-arazzo .ba-diagram.ba-diagram-grown {
  max-height: calc(var(--refs-viewport-height, 100vh) - 140px);
}
.bielu-arazzo .ba-diagram-dragging {
  cursor: grabbing;
}
.bielu-arazzo .ba-diagram-node rect.ba-diagram-spec {
  fill: var(--ba-spec-color);
  stroke: none;
}
/* Under the diagram rather than over it, so it never hides a step box. */
.bielu-arazzo .ba-overview-footer {
  display: flex;
  flex-wrap: wrap;
  align-items: baseline;
  justify-content: space-between;
  gap: 4px 12px;
  margin-top: 6px;
}
.bielu-arazzo .ba-overview-footer .ba-overview-hint {
  margin-top: 0;
}
.bielu-arazzo .ba-diagram-legend {
  display: flex;
  flex-wrap: wrap;
  gap: 4px 12px;
  margin: 0;
  padding: 0;
  color: var(--scalar-color-2);
  font-size: var(--scalar-mini, 12px);
  list-style: none;
}
.bielu-arazzo .ba-diagram-legend li {
  display: flex;
  align-items: center;
  gap: 5px;
}
.bielu-arazzo .ba-diagram-legend-swatch {
  width: 6px;
  height: 14px;
  border-radius: 3px;
  background: var(--ba-spec-color);
}
.bielu-arazzo .ba-diagram-canvas {
  transform-origin: 0 0;
  will-change: transform;
}
.bielu-arazzo .ba-diagram svg {
  display: block;
  /* Sized by the diagram itself (its scale is kept across expanding and collapsing workflows). */
  max-width: none;
  overflow: visible;
}
.bielu-arazzo .ba-diagram-node rect {
  fill: var(--scalar-background-2);
  stroke: var(--scalar-border-color);
  stroke-width: 1;
}
.bielu-arazzo .ba-diagram-node text {
  fill: var(--scalar-color-1);
  font-family: var(--scalar-font-code);
  font-size: 12px;
}
.bielu-arazzo .ba-diagram-link {
  cursor: pointer;
}
/* A step that retries itself: a pill inside the right end of its box, in orange, as a retry is not yet a failure. */
.bielu-arazzo .ba-diagram-node .ba-diagram-retry rect {
  fill: color-mix(in srgb, var(--scalar-color-orange, #c2700c) 12%, var(--scalar-background-1));
  stroke: color-mix(in srgb, var(--scalar-color-orange, #c2700c) 45%, transparent);
}
.bielu-arazzo .ba-diagram-node .ba-diagram-retry text {
  fill: var(--scalar-color-2);
  font-size: 10.5px;
}
.bielu-arazzo .ba-diagram-link:hover rect {
  stroke: var(--scalar-color-accent);
}
.bielu-arazzo .ba-diagram-node-success rect {
  fill: color-mix(in srgb, var(--scalar-color-green, #2e9d4d) 14%, var(--scalar-background-1));
  stroke: var(--scalar-color-green, #2e9d4d);
}
.bielu-arazzo .ba-diagram-node-failure rect {
  fill: color-mix(in srgb, var(--scalar-color-red, #d4413b) 14%, var(--scalar-background-1));
  stroke: var(--scalar-color-red, #d4413b);
}
.bielu-arazzo .ba-diagram-node-success text,
.bielu-arazzo .ba-diagram-node-failure text {
  font-family: var(--scalar-font);
  font-weight: var(--scalar-semibold, 600);
}
.bielu-arazzo .ba-diagram-edge {
  fill: none;
  stroke: var(--scalar-color-3);
  stroke-width: 1.4;
}
.bielu-arazzo .ba-diagram-arrow {
  fill: var(--scalar-color-3);
}
.bielu-arazzo .ba-diagram-edge.ba-diagram-success {
  stroke: var(--scalar-color-green, #2e9d4d);
}
.bielu-arazzo .ba-diagram-arrow.ba-diagram-success {
  fill: var(--scalar-color-green, #2e9d4d);
}
.bielu-arazzo .ba-diagram-edge.ba-diagram-failure {
  stroke: var(--scalar-color-red, #d4413b);
  stroke-dasharray: 5 3;
}
.bielu-arazzo .ba-diagram-arrow.ba-diagram-failure {
  fill: var(--scalar-color-red, #d4413b);
}
/* A workflow a step or an action invokes: a thick outline and italic name set it apart from a step that calls an API. */
.bielu-arazzo .ba-diagram-node-workflow > rect:first-of-type {
  fill: var(--scalar-background-1);
  stroke: var(--scalar-border-color);
  stroke-width: 3;
}
.bielu-arazzo .ba-diagram-node-workflow > text {
  font-style: italic;
}
/* Keyboard focus wins over the workflow, expanded and cycle outlines above and below, which would otherwise hide it. */
.bielu-arazzo .ba-diagram-node.ba-diagram-node:focus-visible > rect:first-of-type {
  stroke: var(--scalar-color-accent);
  stroke-width: 3.5;
  stroke-dasharray: none;
}
.bielu-arazzo .ba-diagram-node-expanded > rect:first-of-type {
  stroke: var(--scalar-color-accent);
}
.bielu-arazzo .ba-diagram-node-cycle > rect:first-of-type {
  stroke-dasharray: 4 3;
}
.bielu-arazzo .ba-diagram-toggle {
  cursor: pointer;
}
.bielu-arazzo .ba-diagram-toggle rect {
  fill: var(--scalar-background-2);
  stroke: var(--scalar-border-color);
  stroke-width: 1;
}
.bielu-arazzo .ba-diagram-toggle text {
  fill: var(--scalar-color-1);
  font-family: var(--scalar-font);
  font-size: 12px;
  font-weight: var(--scalar-semibold, 600);
  font-style: normal;
}
.bielu-arazzo .ba-diagram-toggle:hover rect,
.bielu-arazzo .ba-diagram-toggle:focus-visible rect {
  stroke: var(--scalar-color-accent);
}
.bielu-arazzo .ba-diagram-toggle:focus-visible {
  outline: none;
}
.bielu-arazzo .ba-diagram-toggle-cycle {
  cursor: help;
}
.bielu-arazzo .ba-diagram-group rect {
  fill: color-mix(in srgb, var(--scalar-color-3) 6%, transparent);
  stroke: var(--scalar-border-color);
  stroke-dasharray: 3 3;
  stroke-width: 1;
}
.bielu-arazzo .ba-diagram-group text {
  fill: var(--scalar-color-2);
  font-family: var(--scalar-font-code);
  font-size: 11px;
}
/* Back from a workflow a retry ran to the step that runs again. */
.bielu-arazzo .ba-diagram-edge.ba-diagram-return {
  stroke: var(--scalar-color-orange, #d97706);
  stroke-dasharray: 2 3;
}
.bielu-arazzo .ba-diagram-arrow.ba-diagram-return {
  fill: var(--scalar-color-orange, #d97706);
}
/* From an expanded workflow node into its first steps. */
.bielu-arazzo .ba-diagram-edge.ba-diagram-expand {
  stroke: var(--scalar-color-3);
  stroke-dasharray: 1 3;
}
.bielu-arazzo .ba-nested {
  margin-top: 8px;
}
.bielu-arazzo .ba-nested-steps {
  margin: 8px 0 0;
  padding-left: 20px;
  font-size: var(--scalar-small, 13px);
}
.bielu-arazzo .ba-nested-steps li {
  padding: 2px 0;
}
/* Pages view: a band per page, behind the steps the user takes on it. */
.bielu-arazzo .ba-diagram-band rect {
  fill: color-mix(in srgb, var(--scalar-color-accent) 7%, transparent);
  stroke: color-mix(in srgb, var(--scalar-color-accent) 35%, transparent);
  stroke-width: 1;
}
/* A page title's background: the page under it, so the arrows it sits on pass behind it. */
.bielu-arazzo .ba-diagram-band-title rect {
  fill: color-mix(in srgb, var(--scalar-color-accent) 7%, var(--scalar-background-1));
}
.bielu-arazzo .ba-diagram-band-title.ba-diagram-band-none rect {
  fill: var(--scalar-background-1);
  stroke: none;
}
.bielu-arazzo .ba-diagram-band-title text,
.bielu-arazzo .ba-diagram-band text {
  fill: var(--scalar-color-2);
  font-family: var(--scalar-font);
  font-size: 11px;
  font-weight: var(--scalar-semibold, 600);
}
.bielu-arazzo .ba-diagram-band-none rect {
  fill: color-mix(in srgb, var(--scalar-color-3) 5%, transparent);
  stroke: var(--scalar-border-color);
  stroke-dasharray: 3 3;
}
.bielu-arazzo .ba-diagram-band-none text {
  fill: var(--scalar-color-3);
  font-weight: normal;
  font-style: italic;
}
.bielu-arazzo .ba-diagram-node text.ba-diagram-subtitle {
  fill: var(--scalar-color-2);
  font-family: var(--scalar-font);
  font-size: 10.5px;
  font-weight: normal;
}
.bielu-arazzo .ba-view-toggle {
  display: inline-flex;
  margin-left: auto;
  margin-right: 6px;
  border: var(--scalar-border-width, 1px) solid var(--scalar-border-color);
  border-radius: var(--scalar-radius, 6px);
  overflow: hidden;
}
.bielu-arazzo .ba-view-toggle button {
  padding: 2px 8px;
  border: 0;
  background: transparent;
  color: var(--scalar-color-2);
  font-size: var(--scalar-mini, 12px);
  cursor: pointer;
}
.bielu-arazzo .ba-view-toggle button[aria-pressed='true'] {
  background: var(--scalar-background-3, var(--scalar-background-2));
  color: var(--scalar-color-1);
}
.bielu-arazzo .ba-step-page {
  display: flex;
  flex-wrap: wrap;
  align-items: center;
  gap: 6px;
  margin-bottom: 2px;
  color: var(--scalar-color-2);
  font-size: var(--scalar-small, 13px);
}
.bielu-arazzo .ba-diagram-legend-band {
  width: 14px;
  height: 10px;
  border: 1px solid color-mix(in srgb, var(--scalar-color-accent) 35%, transparent);
  border-radius: 3px;
  background: color-mix(in srgb, var(--scalar-color-accent) 7%, transparent);
}
.bielu-arazzo .ba-diagram-legend-band-none {
  border: 1px dashed var(--scalar-border-color);
  background: transparent;
}
.bielu-arazzo .ba-diagram-legend-end {
  display: inline-flex;
  align-items: center;
  justify-content: center;
  width: 16px;
  height: 12px;
  border-radius: 6px;
  font-size: 9px;
}
.bielu-arazzo .ba-diagram-legend-end-success {
  border: 1px solid var(--scalar-color-green, #2e9d4d);
  color: var(--scalar-color-green, #2e9d4d);
}
.bielu-arazzo .ba-diagram-legend-end-failure {
  border: 1px solid var(--scalar-color-red, #d4413b);
}
.bielu-arazzo .ba-diagram-legend-workflow {
  width: 14px;
  height: 12px;
  border: 3px solid var(--scalar-border-color);
  border-radius: 3px;
  box-sizing: border-box;
}
.bielu-arazzo .ba-diagram-legend-line {
  width: 16px;
  height: 0;
  border-top: 2px dashed var(--ba-line-color);
}
.bielu-arazzo .ba-diagram-edge-label rect {
  fill: var(--scalar-background-1);
  stroke: var(--scalar-border-color);
  stroke-width: 0.5;
}
.bielu-arazzo .ba-diagram-edge-label text {
  fill: var(--scalar-color-2);
  font-family: var(--scalar-font-code);
  font-size: 10.5px;
}
.bielu-arazzo .ba-dialog {
  /* Scalar's CSS reset zeroes the margin a dialog centers itself with. */
  margin: auto;
  /* The whole window: a workflow with an expanded one inside it needs the room. */
  width: 96vw;
  height: 92vh;
  max-width: none;
  max-height: none;
  padding: 16px;
  border: var(--scalar-border-width, 1px) solid var(--scalar-border-color);
  border-radius: var(--scalar-radius-lg, 8px);
  background: var(--scalar-background-1);
  color: var(--scalar-color-1);
  box-shadow: var(--scalar-shadow-2, 0 10px 30px rgb(0 0 0 / 0.25));
}
.bielu-arazzo .ba-dialog[open] {
  display: flex;
  flex-direction: column;
}
.bielu-arazzo .ba-dialog::backdrop {
  background: rgb(0 0 0 / 0.45);
}
.bielu-arazzo .ba-dialog-actions {
  display: flex;
  align-items: center;
  gap: 6px;
}
.bielu-arazzo .ba-diagram.ba-diagram-large {
  flex: 1;
  align-items: center;
  max-height: none;
  min-height: 0;
}
.bielu-arazzo .ba-diagram-large {
  padding: 24px;
}
.bielu-arazzo .ba-diagram-canvas-fit {
  display: flex;
  align-items: center;
  justify-content: center;
  width: 100%;
  height: 100%;
}
/* Taller than the window at a readable size: start from the top, the reader pans down. */
.bielu-arazzo .ba-diagram-canvas-fit.ba-diagram-canvas-top {
  align-items: flex-start;
}
.bielu-arazzo .ba-target-parameters {
  margin-top: 12px;
}
.bielu-arazzo .ba-schema {
  margin: 0;
  padding: 0;
  list-style: none;
}
.bielu-arazzo .ba-schema-property {
  padding: 8px 0;
  border-bottom: var(--scalar-border-width, 1px) solid var(--scalar-border-color);
}
.bielu-arazzo .ba-schema-property:last-child {
  border-bottom: none;
}
.bielu-arazzo .ba-schema-heading {
  display: flex;
  flex-wrap: wrap;
  align-items: baseline;
  gap: 8px;
}
.bielu-arazzo .ba-schema-name {
  font-weight: var(--scalar-semibold, 600);
}
.bielu-arazzo .ba-schema-type,
.bielu-arazzo .ba-schema-detail {
  color: var(--scalar-color-3);
  font-size: var(--scalar-small, 13px);
}
.bielu-arazzo .ba-schema-heading .ba-required {
  margin-left: 0;
}
.bielu-arazzo .ba-schema-description {
  margin-top: 4px;
  font-size: var(--scalar-small, 13px);
}
.bielu-arazzo .ba-schema-children {
  margin-top: 8px;
}
.bielu-arazzo .ba-schema-children > summary {
  display: flex;
  align-items: center;
  gap: 8px;
  width: fit-content;
  padding: 4px 6px;
  border-radius: var(--scalar-radius, 4px);
  color: var(--scalar-color-2);
  font-size: var(--scalar-mini, 12px);
  cursor: pointer;
  list-style: none;
}
.bielu-arazzo .ba-schema-children > summary::-webkit-details-marker {
  display: none;
}
.bielu-arazzo .ba-schema-children[open] > summary {
  margin-bottom: 4px;
}
.bielu-arazzo .ba-schema-children > .ba-schema {
  margin-left: 6px;
  padding-left: 12px;
  border-left: var(--scalar-border-width, 1px) solid var(--scalar-border-color);
}
.bielu-arazzo .ba-step-id {
  font-family: var(--scalar-font-code);
  color: var(--scalar-color-3);
}
.bielu-arazzo .ba-extensions > summary {
  display: flex;
  align-items: center;
  gap: 8px;
  padding: 4px 6px;
  border-radius: var(--scalar-radius, 4px);
  list-style: none;
}
.bielu-arazzo .ba-extensions > summary::-webkit-details-marker {
  display: none;
}
.bielu-arazzo .ba-extensions > summary::before,
.bielu-arazzo .ba-schema-children > summary::before {
  content: '';
  width: 6px;
  height: 6px;
  flex-shrink: 0;
  border-right: 1.5px solid currentColor;
  border-bottom: 1.5px solid currentColor;
  transform: rotate(-45deg);
}
.bielu-arazzo .ba-extensions[open] > summary::before,
.bielu-arazzo .ba-schema-children[open] > summary::before {
  transform: rotate(45deg);
}
.bielu-arazzo .ba-extensions > summary:hover,
.bielu-arazzo .ba-schema-children > summary:hover {
  background: var(--scalar-background-2);
  color: var(--scalar-color-1);
}
.bielu-arazzo :is(button, summary, a):focus-visible {
  outline: 2px solid var(--scalar-color-accent);
  outline-offset: 3px;
}
.bielu-arazzo .ba-toggle,
.bielu-arazzo .ba-icon-button {
  min-height: 28px;
}
.bielu-arazzo .ba-target-header .ba-spec {
  order: 2;
  margin-left: auto;
  margin-right: 0;
  background: none;
  color: var(--scalar-color-3);
}
.bielu-arazzo .ba-target-link,
.bielu-arazzo .ba-target-signature {
  min-width: 0;
  overflow-wrap: anywhere;
}
</style>
