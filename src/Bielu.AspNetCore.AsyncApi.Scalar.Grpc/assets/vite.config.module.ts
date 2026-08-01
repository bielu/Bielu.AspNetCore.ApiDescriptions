import { fileURLToPath, URL } from 'node:url'
import vue from '@vitejs/plugin-vue'
import { defineConfig } from 'vite'

/**
 * Builds the ESM module (`dist/scalar-plugin.mjs`) that Scalar's `pluginUrls` configuration loads.
 *
 * Kept separate from `dist/plugin.mjs`: that one is the package's library entry, built from
 * `src/index.ts` for bundler consumers who import `createGrpcPlugin` themselves, and it registers
 * the console by hooking `window.Scalar`. `pluginUrls` needs the opposite contract — a module whose
 * **default export** is the plugin function, with no self-installation — so it gets its own entry
 * (`src/plugin-module.ts`) and its own file.
 *
 * `emptyOutDir` is off: this build runs after the main build and must not delete its output.
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
