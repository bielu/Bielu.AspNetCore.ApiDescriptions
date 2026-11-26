import { installScalarPluginHook } from '@bielu/scalar-core'
import { createLikeC4Plugin, LIKEC4_ELEMENT_SPEC } from './plugin'

// The `<script>`-tag entry, built as the IIFE `dist/plugin.js` (global `BieluScalarLikeC4`): the package
// root's exports and element registration, plus a hook on `window.Scalar.createApiReference` so every
// Scalar API Reference on the page gets the LikeC4 document type.
export * from './index'

installScalarPluginHook({ pluginName: LIKEC4_ELEMENT_SPEC.pluginName, wrappedFlag: '__bieluLikeC4Wrapped' }, () =>
  createLikeC4Plugin(),
)
