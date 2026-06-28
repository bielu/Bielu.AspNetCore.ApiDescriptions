import { fileURLToPath, URL } from 'node:url'
import vue from '@vitejs/plugin-vue'
import { defineConfig } from 'vite'
import cssInjectedByJs from 'vite-plugin-css-injected-by-js'

/**
 * Builds a single self-contained IIFE bundle (`dist/bielu-scalar-signalr.js`).
 *
 * The bundle wraps `@scalar/api-reference`, registers the SignalR console plugin, and
 * re-exposes `window.Scalar.createApiReference`, so it is a drop-in replacement for the
 * default `scalar.js` bundle. Everything (Vue, Scalar, SignalR client) is bundled in so the
 * file can be served standalone, embedded into the .NET package, or published to a CDN.
 */
export default defineConfig({
  // `cssInjectedByJs` folds the extracted CSS (Scalar's styles + the console's scoped styles) back
  // into the JS so the bundle is a fully self-contained, single-file drop-in for `scalar.js`.
  plugins: [vue(), cssInjectedByJs()],
  resolve: {
    alias: {
      '@': fileURLToPath(new URL('./src', import.meta.url)),
    },
  },
  // Scalar/Vue dependencies reference `process.env.NODE_ENV`; replace it at build time so the
  // browser bundle does not hit `process is not defined`.
  define: {
    'process.env.NODE_ENV': JSON.stringify('production'),
  },
  build: {
    outDir: 'dist',
    emptyOutDir: true,
    lib: {
      entry: fileURLToPath(new URL('./src/index.ts', import.meta.url)),
      name: 'BieluScalarSignalR',
      formats: ['iife'],
      fileName: () => 'bielu-scalar-signalr.js',
    },
    rollupOptions: {
      output: {
        // Single file, no code-splitting, so it can be dropped in as one <script src>.
        inlineDynamicImports: true,
        // Safety net for any bare `process` reference that survives the `define` replacement.
        banner: 'window.process = window.process || { env: { NODE_ENV: "production" } };',
      },
    },
  },
})
