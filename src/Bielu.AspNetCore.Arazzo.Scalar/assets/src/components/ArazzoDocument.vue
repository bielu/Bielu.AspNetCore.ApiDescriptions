<script setup lang="ts">
import { computed, nextTick, onBeforeUnmount, onMounted, ref, watch } from 'vue'
import { renderMarkdown } from '../helpers/markdown'
import { toWorkflowViews } from '../helpers/navigation'
import type { ArazzoDocument, PluginEventBus, PluginNavigationEntry } from '../types'
import ArazzoWorkflow from './ArazzoWorkflow.vue'

/**
 * The content of an Arazzo document in Scalar. Scalar renders it in place of its own introduction
 * and content tree, and hands it the props below (see `documentTypes` in scalar/scalar#10816).
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
  /** Scalar's configuration; not used yet, declared so it is not reflected as an attribute. */
  options?: Record<string, unknown>
}>()

const root = ref<HTMLElement>()

const info = computed(() => props.document?.info ?? {})

const description = computed(() => renderMarkdown(info.value.description))

const workflows = computed(() => (props.document ? toWorkflowViews(props.document, props.navigation ?? []) : []))

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

onMounted(observeSections)
watch(workflows, () => nextTick(observeSections))
onBeforeUnmount(() => observer?.disconnect())
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
      <p
        v-if="info.summary"
        class="ba-summary">
        {{ info.summary }}
      </p>
      <div
        v-if="description"
        class="ba-markdown"
        v-html="description" />

      <div
        v-if="document?.sourceDescriptions?.length"
        class="ba-block">
        <div class="ba-block-title">Source descriptions</div>
        <table class="ba-table">
          <tbody>
            <tr
              v-for="source in document.sourceDescriptions"
              :key="source.name">
              <td><code>{{ source.name }}</code></td>
              <td class="ba-muted">{{ source.type ?? 'openapi' }}</td>
              <td>
                <a
                  :href="source.url"
                  rel="noopener"
                  target="_blank">
                  {{ source.url }}
                </a>
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
      :documents="documents ?? {}"
      :view="view" />

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
  max-width: 1120px;
  margin: 0 auto;
  padding: 48px 60px 96px;
  color: var(--scalar-color-1);
  font-family: var(--scalar-font);
  font-size: var(--scalar-paragraph, 16px);
  line-height: 1.5;
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
  font-family: var(--scalar-font-code);
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
.bielu-arazzo .ba-code {
  margin: 0;
  padding: 10px 12px;
  overflow: auto;
  border-radius: var(--scalar-radius, 4px);
  background: var(--scalar-background-2);
}
</style>
