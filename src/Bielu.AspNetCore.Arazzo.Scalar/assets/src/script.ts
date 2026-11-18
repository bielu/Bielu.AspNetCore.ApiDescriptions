import { installScalarPluginHook } from '@bielu/scalar-core'
import { ARAZZO_ELEMENT_SPEC, createArazzoPlugin } from './plugin'

// The `<script>`-tag entry, built as the IIFE `dist/plugin.js` (global `BieluScalarArazzo`): the package
// root's exports and element registration, plus a hook on `window.Scalar.createApiReference` so every
// Scalar API Reference on the page gets the Arazzo document type.
export * from './index'

installScalarPluginHook({ pluginName: ARAZZO_ELEMENT_SPEC.pluginName, wrappedFlag: '__bieluArazzoWrapped' }, () =>
  createArazzoPlugin(),
)
