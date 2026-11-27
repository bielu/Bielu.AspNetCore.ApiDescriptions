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

import {
  LAYOUTED_STAGE,
  toViewSections,
  viewsOf,
  viewTitle,
} from '../helpers/navigation'
import type {
  LikeC4Model,
  PluginEventBus,
  PluginNavigationEntry,
} from '../types'
import LikeC4ViewSection from './LikeC4ViewSection.vue'

/**
 * The content of a LikeC4 model in Scalar. Scalar renders it in place of its own introduction and content
 * tree, and hands it the props below (see `PluginDocumentType.content` in `@scalar/types`).
 */
const props = defineProps<{
  document?: LikeC4Model
  /** The workspace document name (the slug of the configured source). */
  documentName?: string
  /** The sidebar entries `traverse` built for this document. */
  navigation?: PluginNavigationEntry[]
  eventBus?: PluginEventBus
}>()

const root = ref<HTMLElement>()

/**
 * A part of the model that failed to render. The error stops at this boundary, so the rest of the model
 * still shows, with a note saying what is missing, instead of an empty page.
 */
const renderError = ref<string>()
onErrorCaptured((error) => {
  renderError.value = error instanceof Error ? error.message : String(error)
  console.error(
    '[bielu-likec4] Part of the LikeC4 model could not be rendered.',
    error,
  )
  return false
})

const model = computed(() => props.document)

/** Whether LikeC4 laid the views out: only a laid-out model (`likec4 export json`) has places to draw at. */
const layouted = computed(() => model.value?._stage === LAYOUTED_STAGE)

/**
 * The model's title: its project's, unless that is only the id LikeC4 gives a project without a config
 * (`default`), which says nothing; then the source's name.
 */
const title = computed(() => {
  const project = model.value?.project
  const named =
    project?.title && project.title !== (project.id ?? model.value?.projectId)
  return (named && project?.title) || props.documentName || 'LikeC4 model'
})

const sections = computed(() =>
  layouted.value ? toViewSections(model.value, props.navigation ?? []) : [],
)

const elementCount = computed(
  () => Object.keys(model.value?.elements ?? {}).length,
)

/** Every view's title by id, so an element or a step that opens a view links to its section. */
const viewTitles = computed(() =>
  Object.fromEntries(
    viewsOf(model.value).map((view) => [view.id, viewTitle(view)]),
  ),
)

const customColors = computed(() => model.value?.specification?.customColors)

/** Goes to a view's section, as its sidebar entry does: an element or a step that opens a view leads here. */
const navigate = (viewId: string) => {
  const section = sections.value.find((entry) => entry.view.id === viewId)
  if (!section) {
    return
  }
  document
    .getElementById(section.id)
    ?.scrollIntoView({ behavior: 'smooth', block: 'start' })
  props.eventBus?.emit('intersecting:nav-item', { id: section.id })
}

/**
 * Keeps the sidebar and URL in sync while scrolling, which Scalar leaves to the plugin: the active entry is
 * the last section (in document order) inside the top band of the viewport.
 */
let observer: IntersectionObserver | undefined
const visible = new Set<Element>()

const observeSections = () => {
  observer?.disconnect()
  visible.clear()
  if (!root.value || typeof IntersectionObserver === 'undefined') {
    return
  }
  const elements = Array.from(
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
      const active = elements.filter((element) => visible.has(element)).pop()
      if (active?.dataset.navId) {
        props.eventBus?.emit('intersecting:nav-item', {
          id: active.dataset.navId,
        })
      }
    },
    { rootMargin: '0px 0px -70% 0px' },
  )
  elements.forEach((element) => observer!.observe(element))
}

onMounted(observeSections)
watch(sections, () => nextTick(observeSections))
onBeforeUnmount(() => observer?.disconnect())
</script>

<template>
  <article
    ref="root"
    class="bielu-likec4">
    <header class="bl-header">
      <div class="bl-kicker">
        LikeC4 model
        <template v-if="layouted">
          · {{ sections.length }}
          {{ sections.length === 1 ? 'view' : 'views' }} ·
          {{ elementCount }}
          {{ elementCount === 1 ? 'element' : 'elements' }}
        </template>
      </div>
      <h1 class="bl-title">{{ title }}</h1>
      <p
        v-if="renderError"
        class="bl-render-error"
        role="alert">
        Part of this model could not be shown: {{ renderError }}
      </p>
    </header>

    <p
      v-if="model && !layouted"
      class="bl-note"
      role="status">
      This model is not laid out (its stage is “{{ model._stage }}”), so its
      views have nowhere to be drawn. Write it with
      <code>likec4 export json</code>, which lays every view out.
    </p>
    <p
      v-else-if="!sections.length"
      class="bl-muted">
      This model has no views.
    </p>

    <LikeC4ViewSection
      v-for="section in sections"
      :id="section.id"
      :key="section.id"
      :customColors="customColors"
      :view="section.view"
      :viewTitles="viewTitles"
      @navigate="navigate" />
  </article>
</template>

<!-- The styles for every LikeC4 component, in src/styles/ (see ../style.css for why they live there). -->
<style src="../style.css"></style>
