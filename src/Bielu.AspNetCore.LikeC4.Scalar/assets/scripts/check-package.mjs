import assert from 'node:assert/strict'
import { execFileSync } from 'node:child_process'
import { mkdtempSync, mkdirSync, renameSync, existsSync, rmSync } from 'node:fs'
import { tmpdir } from 'node:os'
import { join, resolve } from 'node:path'
import { createRequire } from 'node:module'
import { pathToFileURL } from 'node:url'

const scratch = mkdtempSync(join(tmpdir(), 'likec4-package-'))
try {
  const output = execFileSync(process.execPath, [process.env.npm_execpath, 'pack', '--ignore-scripts', '--json', '--pack-destination', scratch], { encoding: 'utf8' })
  const [{ filename }] = JSON.parse(output)
  execFileSync('tar', ['-xf', join(scratch, filename), '-C', scratch])
  const scope = join(scratch, 'node_modules', '@bielu')
  mkdirSync(scope, { recursive: true })
  renameSync(join(scratch, 'package'), join(scope, 'scalar-likec4'))
  const require = createRequire(join(scratch, 'consumer.cjs'))
  const root = await import(pathToFileURL(require.resolve('@bielu/scalar-likec4')).href)
  const plugin = await import(pathToFileURL(require.resolve('@bielu/scalar-likec4/scalar-plugin.mjs')).href)
  assert.equal(typeof root.createLikeC4Plugin, 'function')
  assert.equal(typeof plugin.default, 'function')
  assert.ok(existsSync(require.resolve('@bielu/scalar-likec4/plugin.js')))
  assert.ok(existsSync(join(scope, 'scalar-likec4', 'dist', 'types', 'index.d.ts')))
  console.log('Packed LikeC4 package exports and declarations verified.')
} finally {
  assert.equal(resolve(scratch).startsWith(resolve(tmpdir())), true)
  rmSync(scratch, { recursive: true, force: true })
}

