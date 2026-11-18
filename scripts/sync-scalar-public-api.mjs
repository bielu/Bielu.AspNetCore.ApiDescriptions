// Keep generated Scalar CDN constants and optional defaults aligned with npm releases.
import { readFileSync, writeFileSync, readdirSync, existsSync } from 'node:fs'
import { join } from 'node:path'
import { fileURLToPath } from 'node:url'

const root = fileURLToPath(new URL('../', import.meta.url))
const src = join(root, 'src')
const versions = new Map()
for (const project of readdirSync(src)) {
  const manifest = join(src, project, 'assets', 'package.json')
  if (existsSync(manifest)) {
    const { name, version } = JSON.parse(readFileSync(manifest, 'utf8'))
    versions.set(name, version)
  }
}
const update = (entry) => entry.replace(
  /(https:\/\/cdn\.jsdelivr\.net\/npm\/)(@bielu\/[^@/"]+)@([^/"]+)(\/dist\/scalar-plugin\.mjs)/g,
  (url, prefix, name, version, suffix) => versions.has(name) ? prefix + name + '@' + versions.get(name) + suffix : url,
)
for (const project of readdirSync(src)) {
  const shippedPath = join(src, project, 'PublicAPI.Shipped.txt')
  const pendingPath = join(src, project, 'PublicAPI.Unshipped.txt')
  if (!existsSync(shippedPath) || !existsSync(pendingPath)) continue
  const pending = readFileSync(pendingPath, 'utf8')
  const eol = pending.includes('\r\n') ? '\r\n' : '\n'
  const entries = new Set(pending.trimEnd().split(/\r?\n/).map((entry) => entry.startsWith('*REMOVED*') ? entry : update(entry)))
  for (const entry of readFileSync(shippedPath, 'utf8').split(/\r?\n/)) {
    const updated = update(entry)
    if (updated !== entry) {
      entries.add('*REMOVED*' + entry)
      entries.add(updated)
    }
  }
  const output = [...entries].join(eol) + eol
  if (output !== pending) writeFileSync(pendingPath, output)
}

