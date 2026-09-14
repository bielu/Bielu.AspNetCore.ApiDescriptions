import { fileURLToPath, URL } from 'node:url'
import vue from '@vitejs/plugin-vue'
import { defineConfig } from 'vite'

/**
 * Builds a small IIFE script (`dist/plugin.js`) that registers the broker console with Scalar.
 *
 * It does NOT bundle `@scalar/api-reference` — the page still loads Scalar's own bundle (so Scalar
 * styles itself normally). This script only contains the broker console (a Vue Web Component) and
 * hooks `window.Scalar.createApiReference` to add the plugin.
 *
 * There is no broker client library here: a browser cannot speak Kafka, MQTT or AMQP, so the console
 * talks to the server-side bridge over plain `fetch` instead. That is why this bundle is markedly
 * smaller than the gRPC one.
 */
export default defineConfig({
  // `customElement: true` compiles the SFC as a Web Component: its styles are collected into the
  // element's shadow DOM instead of being injected globally, so nothing leaks into Scalar's page.
  plugins: [vue({ customElement: true })],
  resolve: {
    alias: {
      '@': fileURLToPath(new URL('./src', import.meta.url)),
    },
  },
  // Some dependencies read `process.env.*`; there is no `process` global in the browser, so
  // replace it at build time to avoid a `process is not defined` crash.
  define: {
    'process.env.NODE_ENV': JSON.stringify('production'),
    'process.env': '{}',
  },
  build: {
    outDir: 'dist',
    emptyOutDir: true,
    lib: {
      entry: fileURLToPath(new URL('./src/index.ts', import.meta.url)),
      name: 'BieluScalarBroker',
      formats: ['iife', 'es'],
      fileName: (format) => (format === 'es' ? 'plugin.mjs' : 'plugin.js'),
    },
    rollupOptions: {
      output: {
        inlineDynamicImports: true,
      },
    },
  },
})
