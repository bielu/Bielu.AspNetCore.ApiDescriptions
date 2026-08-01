import { fileURLToPath, URL } from 'node:url'
import vue from '@vitejs/plugin-vue'
import { defineConfig } from 'vite'

/**
 * Builds the ESM module (`dist/scalar-plugin.mjs`) that Scalar's `pluginUrls` configuration loads.
 *
 * Same contents as the IIFE `dist/plugin.js` (see vite.config.ts) — the SignalR console Web
 * Component plus the `@microsoft/signalr` client, without `@scalar/api-reference` — but shipped as
 * an ES module with the plugin as its default export, because `pluginUrls` loads each entry with a
 * dynamic `import()` and registers `module.default`.
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
    'process.env': '{}',
  },
  build: {
    outDir: 'dist',
    emptyOutDir: false,
    lib: {
      entry: fileURLToPath(new URL('./src/plugin-module.ts', import.meta.url)),
      formats: ['es'],
      fileName: () => 'scalar-plugin.mjs',
    },
    rollupOptions: {
      output: {
        inlineDynamicImports: true,
      },
    },
  },
})
