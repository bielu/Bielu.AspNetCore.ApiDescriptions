#!/usr/bin/env node
/**
 * Builds `dist/standalone.js`: Scalar's prebuilt browser bundle concatenated with this package's
 * `dist/plugin.js`.
 *
 * plugin.js only hooks `window.Scalar.createApiReference`, so it is useless on a page that never
 * loads Scalar itself. That is exactly the Aspire situation: `ScalarAspireOptions.BundleUrl`
 * REPLACES the container's Scalar bundle, so the URL it points at must be a full bundle (Scalar +
 * console plugin in one script). Concatenating the prebuilt IIFE keeps Scalar's own minified output
 * byte-for-byte instead of re-bundling it through vite (where `sideEffects` hints or re-minification
 * could break it).
 *
 * Run from a console plugin package directory (`npm run` sets the cwd):
 *   node ../../Bielu.AspNetCore.AsyncApi.Scalar/assets/scripts/build-standalone.mjs
 */
import { readFile, writeFile, mkdir, copyFile } from 'node:fs/promises'
import { createRequire } from 'node:module'
import { dirname, join } from 'node:path'

const packageDir = process.cwd()
const require = createRequire(join(packageDir, 'package.json'))
const pkg = require('./package.json')

// The exports map of @scalar/api-reference does not expose the browser bundle, so resolve the
// package's main entry and walk to the sibling dist/browser/standalone.js (its `browser` field).
const scalarEntry = require.resolve('@scalar/api-reference')
const scalarStandalonePath = join(dirname(scalarEntry), 'browser', 'standalone.js')

const pluginPath = join(packageDir, 'dist', 'plugin.js')
const standaloneDir = join(packageDir, 'standalone')
const outputPath = join(standaloneDir, 'index.js')

await mkdir(standaloneDir, { recursive: true })

const [scalar, plugin] = await Promise.all([
  readFile(scalarStandalonePath, 'utf8'),
  readFile(pluginPath, 'utf8'),
])

// Scalar first, so `window.Scalar` is registered by the time the plugin's bootstrap wraps it. The
// newlines guard against a trailing line comment (e.g. a sourceMappingURL) swallowing the joiner.
await writeFile(outputPath, `${scalar}\n;\n${plugin}`, 'utf8')
console.log(`standalone bundle: ${outputPath} (${((scalar.length + plugin.length) / 1024 / 1024).toFixed(1)} MB)`)

// Generate package.json for the standalone package
const standalonePkg = {
  name: `${pkg.name}-standalone`,
  version: pkg.version,
  description: `Standalone browser bundle for ${pkg.name}. Includes Scalar and the plugin in one script.`,
  license: pkg.license,
  author: pkg.author,
  repository: pkg.repository,
  main: "./index.js",
  files: ["index.js", "README.md"]
}

await writeFile(join(standaloneDir, 'package.json'), JSON.stringify(standalonePkg, null, 2), 'utf8')

// Copy README.md if it exists
try {
  await copyFile(join(packageDir, 'README.md'), join(standaloneDir, 'README.md'))
} catch (e) {
  // Ignore if README.md doesn't exist
}
