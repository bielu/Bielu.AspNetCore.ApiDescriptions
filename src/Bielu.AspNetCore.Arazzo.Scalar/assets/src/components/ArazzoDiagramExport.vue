<script setup lang="ts">
import { ScalarDropdown, ScalarDropdownItem } from '@scalar/components/dropdown'
import { ref } from 'vue'

import { downloadBlob, standaloneSvg, svgToPng } from '@bielu/scalar-graph'
import { safeName, toMermaid } from '../helpers/diagram-export'
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

/** What the last action did, read out to screen readers and shown for a moment. */
const status = ref('')
let clear: ReturnType<typeof setTimeout> | undefined
const say = (message: string) => {
  status.value = message
  clearTimeout(clear)
  clear = setTimeout(() => (status.value = ''), 2500)
}

const fileName = (extension: string) =>
  `${safeName(props.name)}-overview.${extension}`

const downloadSvg = () => {
  const svg = props.svg()
  if (svg) {
    downloadBlob(
      new Blob([standaloneSvg(svg, props.layout)], { type: 'image/svg+xml' }),
      fileName('svg'),
    )
    say('SVG downloaded')
  }
}

const downloadPng = async () => {
  const svg = props.svg()
  if (!svg) {
    return
  }
  try {
    downloadBlob(
      await svgToPng(
        standaloneSvg(svg, props.layout),
        props.layout.width,
        props.layout.height,
      ),
      fileName('png'),
    )
    say('PNG downloaded')
  } catch {
    say('The PNG could not be made; try SVG')
  }
}

const copyMermaid = async () => {
  try {
    await navigator.clipboard.writeText(toMermaid(props.layout))
    say('Mermaid copied')
  } catch {
    // No clipboard (an insecure page, or permission refused): offer the file instead.
    downloadBlob(
      new Blob([toMermaid(props.layout)], { type: 'text/plain' }),
      fileName('mmd'),
    )
    say('Mermaid downloaded')
  }
}
</script>

<template>
  <!-- Scalar's dropdown: it closes itself after a pick, on Escape and on a click outside. -->
  <ScalarDropdown placement="bottom-end">
    <button
      aria-label="Export workflow diagram"
      class="ba-icon-button"
      title="Export"
      type="button">
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
    </button>
    <template #items>
      <ScalarDropdownItem @click="downloadSvg">Download SVG</ScalarDropdownItem>
      <ScalarDropdownItem @click="downloadPng">Download PNG</ScalarDropdownItem>
      <ScalarDropdownItem @click="copyMermaid"
        >Copy as Mermaid</ScalarDropdownItem
      >
    </template>
  </ScalarDropdown>
  <span
    aria-live="polite"
    class="ba-muted ba-export-status"
    >{{ status }}</span
  >
</template>
