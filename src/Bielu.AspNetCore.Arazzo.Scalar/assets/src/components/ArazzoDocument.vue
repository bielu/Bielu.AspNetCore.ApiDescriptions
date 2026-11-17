<script setup lang="ts">
import { computed, nextTick, onBeforeUnmount, onMounted, ref, watch } from 'vue'
import { renderMarkdown } from '../helpers/markdown'
import { componentsEntryId, toWorkflowViews } from '../helpers/navigation'
import { countStepsBySpec, specLabel, summarizeSources } from '../helpers/target-spec'
import type { ArazzoDocument, PluginEventBus, PluginNavigationEntry } from '../types'
import ArazzoComponents from './ArazzoComponents.vue'
import ArazzoExtensions from './ArazzoExtensions.vue'
import ArazzoWorkflow from './ArazzoWorkflow.vue'

/**
 * The content of an Arazzo document in Scalar. Scalar renders it in place of its own introduction
 * and content tree, and hands it the props below (see `PluginDocumentType.content` in `@scalar/types`).
 */
const props = defineProps<{
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

const info = computed(() => props.document?.info ?? {})

const description = computed(() => renderMarkdown(info.value.description))

const workflows = computed(() => (props.document ? toWorkflowViews(props.document, props.navigation ?? []) : []))

/** The document's own extensions and its `info`'s, shown together in the header (the root's win on a clash). */
const documentExtensions = computed(() => ({ ...info.value, ...props.document }))

/** workflowId → its section's sidebar id, so a step that invokes a workflow links to it. */
const workflowNavIds = computed(() => Object.fromEntries(workflows.value.map((view) => [view.workflow.workflowId, view.id])))

/** The components section's sidebar id; `undefined` when the document declares no components. */
const componentsId = computed(() => componentsEntryId(props.navigation ?? []))

/** Each source description with its spec, whether it is loaded, and how many steps use it. */
const sources = computed(() => (props.document ? summarizeSources(props.document, loadedDocuments.value) : []))

/** "3 OpenAPI steps · 2 AsyncAPI steps": how the workflows split between the specs. */
const stepsBySpec = computed(() => (props.document ? countStepsBySpec(props.document, loadedDocuments.value) : []))

/**
 * The workspace documents, re-read whenever one is added. Scalar loads the sibling documents after
 * this one renders and adds them to the same object in place; that object is reactive in Scalar's
 * Vue, not in this element's own Vue, so nothing re-renders on its own. Comparing the document names
 * once a second is cheap and catches documents added at any time (e.g. after a config change).
 */
const loadedDocuments = ref<Record<string, unknown>>({ ...props.documents })
const documentNames = () => Object.keys(props.documents ?? {}).join('\n')
let lastDocumentNames = documentNames()
const refreshDocuments = () => {
  const names = documentNames()
  if (names !== lastDocumentNames) {
    lastDocumentNames = names
    loadedDocuments.value = { ...props.documents }
  }
}
watch(
  () => props.documents,
  () => {
    lastDocumentNames = documentNames()
    loadedDocuments.value = { ...props.documents }
  },
)
let documentsTimer: ReturnType<typeof setInterval> | undefined

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
  documentsTimer = setInterval(refreshDocuments, 1000)
})
watch(workflows, () => nextTick(observeSections))
onBeforeUnmount(() => {
  observer?.disconnect()
  clearInterval(documentsTimer)
})
</script>

<template>
  <article
    ref="root"
    class="bielu-arazzo">
    <header class="ba-header">
      <div class="ba-kicker">
        Arazzo {{ document?.arazzo }}
        <template v-if="info.version">· v{{ info.version }}</template>
      </div>
      <h1 class="ba-title">{{ info.title || documentName }}</h1>
      <div
        v-if="document?.$self"
        class="ba-self ba-muted">
        Canonical URL
        <a
          :href="document.$self"
          rel="noopener"
          target="_blank">
          {{ document.$self }}
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
        v-if="document?.sourceDescriptions?.length"
        class="ba-block">
        <div class="ba-block-title ba-sources-title">
          Source descriptions
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
                {{ summary.documentName ? 'loaded' : 'not loaded' }} ·
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
    </header>

    <ArazzoWorkflow
      v-for="view in workflows"
      :key="view.id"
      :document="document!"
      :documents="loadedDocuments"
      :eventBus="eventBus"
      :layout="layout"
      :view="view"
      :workflowNavIds="workflowNavIds" />

    <ArazzoComponents
      v-if="document?.components && componentsId"
      :id="componentsId"
      :components="document.components"
      :document="document"
      :eventBus="eventBus" />

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
  text-transform: uppercase;
  letter-spacing: 0.02em;
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
  margin-top: 64px;
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
  counter-reset: ba-step;
}
.bielu-arazzo .ba-steps > li {
  position: relative;
  padding-left: 40px;
  counter-increment: ba-step;
}
.bielu-arazzo .ba-steps > li::before {
  content: counter(ba-step);
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
  border: var(--scalar-border-width, 1px) solid var(--ba-spec-color);
  border-radius: 999px;
  background: color-mix(in srgb, var(--ba-spec-color) 12%, transparent);
  color: var(--scalar-color-1);
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
}
.bielu-arazzo .ba-nowrap {
  white-space: nowrap;
}
.bielu-arazzo .ba-target {
  margin: 12px 0;
  padding: 10px 12px;
  border: var(--scalar-border-width, 1px) solid var(--scalar-border-color);
  border-radius: var(--scalar-radius-lg, 8px);
  background: var(--scalar-background-2);
}
.bielu-arazzo .ba-target-unresolved {
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
.bielu-arazzo .ba-selector {
  display: inline-flex;
  flex-wrap: wrap;
  align-items: baseline;
  gap: 0 4px;
  white-space: normal;
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
  letter-spacing: 0.02em;
  text-transform: uppercase;
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
  max-width: 100%;
  height: auto;
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
  width: min(1440px, 94vw);
  height: min(900px, 88vh);
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
  display: inline-block;
  padding: 2px 10px;
  border: var(--scalar-border-width, 1px) solid var(--scalar-border-color);
  border-radius: 999px;
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
</style>
