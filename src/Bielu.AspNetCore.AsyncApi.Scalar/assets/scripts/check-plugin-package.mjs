// Packs the document-type plugin package in the current folder, installs the tarball into a scratch
// node_modules, and checks what a consumer resolves: the root's factory export, the `pluginUrls` module's
// default export, the IIFE script and the declarations.
//
//   node ../../Bielu.AspNetCore.AsyncApi.Scalar/assets/scripts/check-plugin-package.mjs createArazzoPlugin
import assert from 'node:assert/strict'
import { execFileSync } from 'node:child_process'
import { existsSync, mkdirSync, mkdtempSync, readFileSync, renameSync, rmSync } from 'node:fs'
import { createRequire } from 'node:module'
import { tmpdir } from 'node:os'
import { join, resolve } from 'node:path'
import { pathToFileURL } from 'node:url'

const factoryExport = process.argv[2]
assert.ok(factoryExport, 'usage: check-plugin-package.mjs <factoryExport>')
const { name } = JSON.parse(readFileSync('package.json', 'utf8'))
const [scopeName, packageName] = name.split('/')

const scratch = mkdtempSync(join(tmpdir(), `${packageName}-`))
try {
  const output = execFileSync(process.execPath, [process.env.npm_execpath, 'pack', '--ignore-scripts', '--json', '--pack-destination', scratch], { encoding: 'utf8' })
  const [{ filename }] = JSON.parse(output)
  // Relative to `cwd`: GNU tar (Git for Windows) reads the `C:` of an absolute Windows path as a remote host.
  execFileSync('tar', ['-xf', filename], { cwd: scratch })
  const scope = join(scratch, 'node_modules', scopeName)
  mkdirSync(scope, { recursive: true })
  renameSync(join(scratch, 'package'), join(scope, packageName))
  const require = createRequire(join(scratch, 'consumer.cjs'))
  const root = await import(pathToFileURL(require.resolve(name)).href)
  const plugin = await import(pathToFileURL(require.resolve(`${name}/scalar-plugin.mjs`)).href)
  assert.equal(typeof root[factoryExport], 'function')
  assert.equal(typeof plugin.default, 'function')
  assert.ok(existsSync(require.resolve(`${name}/plugin.js`)))
  assert.ok(existsSync(join(scope, packageName, 'dist', 'types', 'index.d.ts')))
  console.log(`Packed ${name} exports and declarations verified.`)
} finally {
  assert.equal(resolve(scratch).startsWith(resolve(tmpdir())), true)
  rmSync(scratch, { recursive: true, force: true })
}
