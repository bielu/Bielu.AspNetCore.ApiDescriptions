import { fileURLToPath, URL } from 'node:url'
import tailwindcss from '@tailwindcss/vite'
import vue from '@vitejs/plugin-vue'
import { defineConfig } from 'vite'

/**
 * Builds a small IIFE script (`dist/plugin.js`) that registers the LikeC4 document type with Scalar.
 *
 * It does NOT bundle `@scalar/api-reference`: the page still loads Scalar's own bundle. This script
 * only contains the LikeC4 view renderer (a Vue Web Component) and hooks
 * `window.Scalar.createApiReference` to add the plugin.
 */
export default defineConfig({
  // `customElement: true` compiles the SFCs' styles to strings the element registration injects.
  // `tailwindcss()` compiles src/style.css (on Scalar's theme, see @bielu/scalar-core's tailwind.css).
  plugins: [vue({ customElement: true }), tailwindcss()],
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
      entry: fileURLToPath(new URL('./src/script.ts', import.meta.url)),
      name: 'BieluScalarLikeC4',
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
