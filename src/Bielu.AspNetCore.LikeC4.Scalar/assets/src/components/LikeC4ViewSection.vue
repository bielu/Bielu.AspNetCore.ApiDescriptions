<script setup lang="ts">
import { computed, ref } from 'vue'

import { renderMarkdown } from '../helpers/markdown'
import { viewTitle } from '../helpers/navigation'
import { dynamicSteps, isSequence, viewBounds } from '../helpers/view-layout'
import type { LikeC4CustomColor, LikeC4View } from '../types'
import LikeC4Diagram from './LikeC4Diagram.vue'
import LikeC4SequenceDiagram from './LikeC4SequenceDiagram.vue'

/**
 * One view of the model: its title and description, the view drawn as LikeC4 laid it out, and for a dynamic
 * view its steps in order, with their notes.
 */
const props = defineProps<{
  /** The sidebar entry id, which is also the section's id, so Scalar can scroll to it. */
  id: string
  view: LikeC4View
  customColors?: Record<string, LikeC4CustomColor>
  viewTitles: Record<string, string>
}>()

const emit = defineEmits<{ navigate: [viewId: string] }>()

const title = computed(() => viewTitle(props.view))
const description = computed(() => renderMarkdown(props.view.description))
const sequence = computed(() => isSequence(props.view))
const steps = computed(() => dynamicSteps(props.view))

/** What kind of view this is, as LikeC4 calls it. */
const kind = computed(() => {
  switch (props.view._type) {
    case 'dynamic':
      return sequence.value ? 'Dynamic view · sequence' : 'Dynamic view'
    case 'deployment':
      return 'Deployment view'
    default:
      return 'View'
  }
})

/**
 * Whether the drawing is fitted to the page's width or drawn at its natural size. A view narrower than the
 * page is drawn at its natural size either way; a wider one starts fitted, and can be shown at full size to
 * read it, scrolling sideways.
 */
const fitted = ref(true)
const width = computed(() => viewBounds(props.view).width)

const opens = (viewId: string | null | undefined) =>
  viewId && viewId !== props.view.id ? props.viewTitles[viewId] : undefined
</script>

<template>
  <section
    :id="id"
    class="bl-view"
    :data-nav-id="id">
    <div class="bl-view-kicker">
      {{ kind }} · <code>{{ view.id }}</code>
    </div>
    <h2 class="bl-view-title">{{ title }}</h2>
    <div
      v-if="description"
      class="bl-markdown"
      v-html="description" />
    <ul
      v-if="view.links?.length"
      class="bl-links">
      <li
        v-for="link in view.links"
        :key="link.url">
        <a
          :href="link.url"
          rel="noopener"
          target="_blank">
          {{ link.title || link.url }}
        </a>
      </li>
    </ul>

    <div class="bl-canvas-toolbar">
      <button
        :aria-pressed="!fitted"
        class="bl-button"
        type="button"
        @click="fitted = !fitted">
        {{ fitted ? 'Actual size' : 'Fit to width' }}
      </button>
    </div>
    <div
      class="bl-canvas"
      :class="{ 'bl-canvas-fitted': fitted }"
      :style="{ '--bl-natural-width': `${width + 32}px` }">
      <LikeC4SequenceDiagram
        v-if="sequence"
        :customColors="customColors"
        :view="view"
        :viewTitles="viewTitles"
        @navigate="emit('navigate', $event)" />
      <LikeC4Diagram
        v-else
        :customColors="customColors"
        :view="view"
        :viewTitles="viewTitles"
        @navigate="emit('navigate', $event)" />
    </div>

    <ol
      v-if="steps.length"
      class="bl-steps">
      <li
        v-for="step in steps"
        :key="step.edge.id"
        :data-edge-id="step.edge.id">
        <span class="bl-step-number">{{ step.number }}</span>
        <div class="bl-step-body">
          <div class="bl-step-title">
            {{ step.edge.label || `${step.edge.source} → ${step.edge.target}` }}
            <span
              v-if="step.edge.technology"
              class="bl-muted">
              [{{ step.edge.technology }}]
            </span>
          </div>
          <div class="bl-muted">
            <code>{{ step.edge.source }}</code> →
            <code>{{ step.edge.target }}</code>
          </div>
          <div
            v-if="step.edge.notes"
            class="bl-markdown"
            v-html="renderMarkdown(step.edge.notes)" />
          <button
            v-if="opens(step.edge.navigateTo)"
            class="bl-link-button"
            type="button"
            @click="emit('navigate', step.edge.navigateTo!)">
            Opens {{ opens(step.edge.navigateTo) }} →
          </button>
        </div>
      </li>
    </ol>
  </section>
</template>
