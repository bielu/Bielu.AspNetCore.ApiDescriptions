<script setup lang="ts">
import type { PluginEventBus } from '../types'

/**
 * Wraps a heading with a link button that copies the URL of its sidebar entry, like the anchors on
 * Scalar's own headings. Scalar builds the URL, so it matches what the sidebar links to.
 */
const props = defineProps<{
  navId: string
  eventBus?: PluginEventBus
}>()

const copy = () => props.eventBus?.emit('copy-url:nav-item', { id: props.navId })
</script>

<template>
  <div class="ba-anchor">
    <slot />
    <button
      v-if="eventBus"
      aria-label="Copy link"
      class="ba-anchor-button"
      title="Copy link"
      type="button"
      @click="copy">
      #
    </button>
  </div>
</template>
