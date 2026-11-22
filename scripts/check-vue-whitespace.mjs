// Fails when a change to a Scalar plugin's .vue template changes how many spaces it renders between elements.
//
// Vue's compiler drops whitespace between two elements once that whitespace contains a newline, while Prettier
// treats it by HTML rules and may move `<code>a</code> <span>b</span>` onto two lines. The rendered space then
// disappears without any formatting check noticing. This compiles each changed template at the base ref and in
// the working tree and compares the number of whitespace-only text nodes. An intended change of spacing is
// written as `{{ ' ' }}`, which this counts as a space too.
//
// Usage: node scripts/check-vue-whitespace.mjs <base-ref>   (default: origin/main)
import { execFileSync } from 'node:child_process'
import { readFileSync } from 'node:fs'
import { createRequire } from 'node:module'
import path from 'node:path'

const require = createRequire(path.resolve('src/Bielu.AspNetCore.Arazzo.Scalar/assets/package.json'))
const sfc = require('@vue/compiler-sfc')

const git = (...args) => execFileSync('git', args, { encoding: 'utf8', maxBuffer: 1e8 })

// Compare with where this branch left the base, so changes made on the base since then do not count.
const base = git('merge-base', process.argv[2] ?? 'origin/main', 'HEAD').trim()

/** The template's whitespace-only text nodes; `_createTextVNode()` with no argument is a single space. */
const spaces = (source, file) => {
  const { descriptor } = sfc.parse(source, { filename: file })
  if (!descriptor.template) {
    return 0
  }
  const { code } = sfc.compileTemplate({
    source: descriptor.template.content,
    filename: file,
    id: file,
    compilerOptions: { hoistStatic: false },
  })
  return (code.match(/_createTextVNode\(\)|_createTextVNode\("\s+"\)|_toDisplayString\(' '\)/g) ?? []).length
}

const changed = git('diff', '--name-only', '--diff-filter=M', base, '--', 'src/**/assets/src/**/*.vue')
  .split('\n')
  .filter(Boolean)

const problems = []
for (const file of changed) {
  const before = spaces(git('show', `${base}:${file}`), file)
  const after = spaces(readFileSync(file, 'utf8'), file)
  if (before !== after) {
    problems.push(`${file}: renders ${after} spaces between elements, ${base} renders ${before}`)
  }
}

if (problems.length > 0) {
  console.error(problems.join('\n'))
  console.error(
    "A space between elements was probably moved onto its own line, where Vue drops it. Write it as {{ ' ' }}.",
  )
  process.exit(1)
}
console.log(`${changed.length} changed .vue file(s): rendered spaces between elements unchanged.`)
