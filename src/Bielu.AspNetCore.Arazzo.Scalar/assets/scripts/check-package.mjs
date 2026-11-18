import assert from 'node:assert/strict'
import { execFileSync } from 'node:child_process'
import { mkdtempSync, mkdirSync, renameSync, existsSync, rmSync } from 'node:fs'
import { tmpdir } from 'node:os'
import { join, resolve } from 'node:path'
import { createRequire } from 'node:module'
import { pathToFileURL } from 'node:url'

const scratch = mkdtempSync(join(tmpdir(), 'arazzo-package-'))
try {
  const output = execFileSync(process.execPath, [process.env.npm_execpath, 'pack', '--ignore-scripts', '--json', '--pack-destination', scratch], { encoding: 'utf8' })
  const [{ filename }] = JSON.parse(output)
  execFileSync('tar', ['-xf', join(scratch, filename), '-C', scratch])
  const scope = join(scratch, 'node_modules', '@bielu')
  mkdirSync(scope, { recursive: true })
  renameSync(join(scratch, 'package'), join(scope, 'scalar-arazzo'))
  const require = createRequire(join(scratch, 'consumer.cjs'))
  const root = await import(pathToFileURL(require.resolve('@bielu/scalar-arazzo')).href)
  const plugin = await import(pathToFileURL(require.resolve('@bielu/scalar-arazzo/scalar-plugin.mjs')).href)
  assert.equal(typeof root.createArazzoPlugin, 'function')
  assert.equal(typeof plugin.default, 'function')
  assert.ok(existsSync(require.resolve('@bielu/scalar-arazzo/plugin.js')))
  assert.ok(existsSync(join(scope, 'scalar-arazzo', 'dist', 'types', 'index.d.ts')))
  console.log('Packed Arazzo package exports and declarations verified.')
} finally {
  assert.equal(resolve(scratch).startsWith(resolve(tmpdir())), true)
  rmSync(scratch, { recursive: true, force: true })
}

