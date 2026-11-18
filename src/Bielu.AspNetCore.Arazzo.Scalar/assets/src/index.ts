import { registerConsoleElement } from '@bielu/scalar-core'
import ArazzoDocument from './components/ArazzoDocument.vue'
import { ARAZZO_ELEMENT_SPEC } from './plugin'

export { isArazzoDocument, traverseArazzoDocument } from './helpers/navigation'
export { resolveStepTarget } from './helpers/resolve-target'
export type { ChannelTarget, OperationTarget, StepTarget, WorkflowTarget } from './helpers/resolve-target'
export { arazzoDocumentType, createArazzoPlugin } from './plugin'
export type { ApiReferencePlugin, ArazzoDocumentType } from './plugin'
export type { ArazzoPluginOptions } from './helpers/plugin-options'
export type { ArazzoDocument, ArazzoStep, ArazzoWorkflow, PluginNavigationEntry } from './types'

// The package root (`import { createArazzoPlugin } from '@bielu/scalar-arazzo'`, built as `dist/index.mjs`):
// register the element the document type renders, and leave adding the plugin to the caller. The
// `<script>`-tag entry (src/script.ts) also hooks `window.Scalar.createApiReference`.
registerConsoleElement(ARAZZO_ELEMENT_SPEC, ArazzoDocument)
