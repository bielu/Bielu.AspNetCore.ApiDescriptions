import { installScalarPluginHook, registerConsoleElement } from '@bielu/scalar-core'
import ArazzoDocument from './components/ArazzoDocument.vue'
import { ARAZZO_ELEMENT_SPEC, createArazzoPlugin } from './plugin'

export { isArazzoDocument, traverseArazzoDocument } from './helpers/navigation'
export { resolveStepTarget } from './helpers/resolve-target'
export type { ChannelTarget, OperationTarget, StepTarget, WorkflowTarget } from './helpers/resolve-target'
export { arazzoDocumentType, createArazzoPlugin } from './plugin'
export type { ApiReferencePlugin, ArazzoDocumentType } from './plugin'
export type { ArazzoDocument, ArazzoStep, ArazzoWorkflow, PluginNavigationEntry } from './types'

// The `<script>`-tag entry: register the document element and hook `window.Scalar.createApiReference`
// so every Scalar API Reference on the page gets the Arazzo document type.
registerConsoleElement(ARAZZO_ELEMENT_SPEC, ArazzoDocument)
installScalarPluginHook({ pluginName: ARAZZO_ELEMENT_SPEC.pluginName, wrappedFlag: '__bieluArazzoWrapped' }, () =>
  createArazzoPlugin(),
)
