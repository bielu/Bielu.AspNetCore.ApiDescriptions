<script setup lang="ts">
import { ref } from 'vue'
import { downloadBlob, safeName, standaloneSvg, svgToPng, toMermaid } from '../helpers/diagram-export'
import type { WorkflowLayout } from '../helpers/workflow-graph'

/**
 * The overview's export menu: download it as SVG or PNG, or copy it as Mermaid source. The SVG and PNG are taken
 * from the rendered diagram, so they show what is on screen: the same view, expansions and theme.
 */
const props = defineProps<{
  layout: WorkflowLayout
  /** The rendered overview's `<svg>`. */
  svg: () => SVGSVGElement | undefined
  /** The workflow's id, for the file names. */
  name: string
}>()

const menu = ref<HTMLDetailsElement>()

/** What the last action did, read out to screen readers and shown for a moment. */
const status = ref('')
let clear: ReturnType<typeof setTimeout> | undefined
const say = (message: string) => {
  status.value = message
  clearTimeout(clear)
  clear = setTimeout(() => (status.value = ''), 2500)
}

const fileName = (extension: string) => `${safeName(props.name)}-overview.${extension}`

const close = () => menu.value?.removeAttribute('open')

const downloadSvg = () => {
  const svg = props.svg()
  if (svg) {
    downloadBlob(new Blob([standaloneSvg(svg, props.layout)], { type: 'image/svg+xml' }), fileName('svg'))
    say('SVG downloaded')
  }
  close()
}

const downloadPng = async () => {
  const svg = props.svg()
  close()
  if (!svg) {
    return
  }
  try {
    downloadBlob(await svgToPng(standaloneSvg(svg, props.layout), props.layout.width, props.layout.height), fileName('png'))
    say('PNG downloaded')
  } catch {
    say('The PNG could not be made; try SVG')
  }
}

const copyMermaid = async () => {
  close()
  try {
    await navigator.clipboard.writeText(toMermaid(props.layout))
    say('Mermaid copied')
  } catch {
    // No clipboard (an insecure page, or permission refused): offer the file instead.
    downloadBlob(new Blob([toMermaid(props.layout)], { type: 'text/plain' }), fileName('mmd'))
    say('Mermaid downloaded')
  }
}
</script>

<template>
  <details
    ref="menu"
    class="ba-export">
    <summary
      aria-label="Export workflow diagram"
      class="ba-icon-button"
      title="Export">
      <svg
        aria-hidden="true"
        fill="none"
        height="14"
        stroke="currentColor"
        stroke-linecap="round"
        stroke-linejoin="round"
        stroke-width="1.6"
        viewBox="0 0 16 16"
        width="14">
        <path d="M8 2v8M4.5 6.5 8 10l3.5-3.5M3 13h10" />
      </svg>
    </summary>
    <div
      class="ba-export-menu"
      role="menu">
      <button
        role="menuitem"
        type="button"
        @click="downloadSvg">Download SVG</button>
      <button
        role="menuitem"
        type="button"
        @click="downloadPng">Download PNG</button>
      <button
        role="menuitem"
        type="button"
        @click="copyMermaid">Copy as Mermaid</button>
    </div>
  </details>
  <span
    aria-live="polite"
    class="ba-muted ba-export-status">{{ status }}</span>
</template>
