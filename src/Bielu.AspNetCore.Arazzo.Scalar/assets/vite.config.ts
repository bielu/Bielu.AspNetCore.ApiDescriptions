import { fileURLToPath, URL } from 'node:url'
import vue from '@vitejs/plugin-vue'
import { defineConfig } from 'vite'

/**
 * Builds a small IIFE script (`dist/plugin.js`) that registers the Arazzo document type with Scalar.
 *
 * It does NOT bundle `@scalar/api-reference`: the page still loads Scalar's own bundle. This script
 * only contains the Arazzo document view (a Vue Web Component) and hooks
 * `window.Scalar.createApiReference` to add the plugin.
 */
export default defineConfig({
  // `customElement: true` compiles the SFCs' styles to strings the element registration injects.
  plugins: [vue({ customElement: true })],
  resolve: {
    alias: {
      '@': fileURLToPath(new URL('./src', import.meta.url)),
    },
  },
  // Vue reads `process.env.NODE_ENV`; there is no `process` global in the browser.
  define: {
    'process.env.NODE_ENV': JSON.stringify('production'),
  },
  build: {
    outDir: 'dist',
    emptyOutDir: true,
    lib: {
      entry: fileURLToPath(new URL('./src/index.ts', import.meta.url)),
      name: 'BieluScalarArazzo',
      formats: ['iife'],
      fileName: () => 'plugin.js',
    },
    rollupOptions: {
      output: {
        inlineDynamicImports: true,
      },
    },
  },
})
