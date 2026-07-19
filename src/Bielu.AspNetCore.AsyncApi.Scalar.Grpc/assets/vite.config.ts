import { fileURLToPath, URL } from 'node:url'
import vue from '@vitejs/plugin-vue'
import { defineConfig } from 'vite'

/**
 * Builds a small IIFE script (`dist/plugin.js`) that registers the gRPC console with Scalar.
 *
 * It does NOT bundle `@scalar/api-reference` — the page still loads Scalar's own bundle (so Scalar
 * styles itself normally). This script only contains the gRPC console (a Vue Web Component) plus
 * the connect-web gRPC-Web client, and hooks `window.Scalar.createApiReference` to add the plugin.
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
      name: 'BieluScalarGrpc',
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
