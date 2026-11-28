import tailwindcss from '@tailwindcss/vite'
import vue from '@vitejs/plugin-vue'
import { defineConfig } from 'vite'
import { scalarPluginBuild } from '@bielu/scalar-core/vite'

/**
 * Builds the Arazzo document type's three bundles (see @bielu/scalar-core's vite.mjs):
 * `vite build` writes the IIFE `dist/plugin.js`, `--mode module` the `pluginUrls` module
 * `dist/scalar-plugin.mjs`, and `--mode library` the package root `dist/index.mjs`.
 */
export default defineConfig(({ mode }) =>
  scalarPluginBuild({
    root: new URL('.', import.meta.url),
    globalName: 'BieluScalarArazzo',
    mode,
    // `customElement: true` compiles the SFCs' styles to strings the element registration injects.
    plugins: [vue({ customElement: true }), tailwindcss()],
  }),
)
