import { fileURLToPath, URL } from 'node:url'
import vue from '@vitejs/plugin-vue'
import { defineConfig } from 'vite'

/**
 * Builds the package root (`dist/index.mjs`): an ES module with the named exports the README imports
 * (`import { createArazzoPlugin } from '@bielu/scalar-arazzo'`).
 *
 * Same contents as the IIFE `dist/plugin.js` (see vite.config.ts), without the hook on
 * `window.Scalar.createApiReference`: whoever imports the package adds the plugin themselves.
 *
 * `emptyOutDir` is off: this build runs after the IIFE build and must not delete `dist/plugin.js`.
 */
export default defineConfig({
  plugins: [vue({ customElement: true })],
  resolve: {
    alias: {
      '@': fileURLToPath(new URL('./src', import.meta.url)),
    },
  },
  define: {
    'process.env.NODE_ENV': JSON.stringify('production'),
  },
  build: {
    outDir: 'dist',
    emptyOutDir: false,
    lib: {
      entry: fileURLToPath(new URL('./src/index.ts', import.meta.url)),
      formats: ['es'],
      fileName: () => 'index.mjs',
    },
    rollupOptions: {
      output: {
        inlineDynamicImports: true,
      },
    },
  },
})
