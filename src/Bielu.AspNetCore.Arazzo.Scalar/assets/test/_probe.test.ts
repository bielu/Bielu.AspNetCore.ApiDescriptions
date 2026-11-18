import { readFileSync } from 'node:fs'
import { it } from 'vitest'
import { buildWorkflowGraph, groupId, layoutWorkflowGraph } from '../src/helpers/workflow-graph'

const doc = JSON.parse(readFileSync('C:/Users/bielu/AppData/Local/Temp/claude/6d1f3a61a420f37d/7c5ed19b-5731-40d4-888d-4f1bc5cea594/scratchpad/bands/v1.json', 'utf8'))
const pages = { pagesExtension: 'x-shop-pages', pageExtension: 'x-page', labelExtension: 'x-label' }
type R = { x: number; y: number; width: number; height: number }
const l = (b: R) => b.x - b.width / 2, r = (b: R) => b.x + b.width / 2, t = (b: R) => b.y - b.height / 2, bo = (b: R) => b.y + b.height / 2

it('probe', () => {
  for (const view of ['pages', 'technical'] as const) {
    for (const wf of doc.workflows) {
      // Every combination along a growing set: none, each single node, then all.
      let expanded = new Set<string>()
      const sets: Set<string>[] = [new Set()]
      for (;;) {
        const g = buildWorkflowGraph(doc, wf, undefined, { view, pages, expanded })
        const more = g.nodes.filter((n) => n.expandable && !expanded.has(n.id)).map((n) => n.id)
        if (!more.length) break
        for (const id of more) sets.push(new Set([...expanded, id]))
        expanded = new Set([...expanded, ...more])
        sets.push(expanded)
      }
      for (const set of sets) {
        const layout = layoutWorkflowGraph(buildWorkflowGraph(doc, wf, undefined, { view, pages, expanded: set }))
        for (const band of layout.bands) {
          const group = band.group ? layout.groups.find((x) => x.id === band.group) : undefined
          const outer: R = group ?? { x: layout.width / 2, y: layout.height / 2, width: layout.width, height: layout.height }
          const d = { left: l(outer) - l(band), right: r(band) - r(outer), top: t(outer) - t(band), bottom: bo(band) - bo(outer) }
          const bad = Object.entries(d).filter(([, v]) => v > 0.5)
          if (bad.length) console.log(view, wf.workflowId, [...set].join('+') || '-', band.id, band.label, 'in', group?.id ?? 'diagram', JSON.stringify(Object.fromEntries(bad.map(([k, v]) => [k, Math.round(v)]))))
        }
        // Groups inside their parents too.
        for (const group of layout.groups) {
          const parent = group.parent ? layout.groups.find((x) => x.id === group.parent)! : undefined
          if (parent && (l(group) < l(parent) - 0.5 || r(group) > r(parent) + 0.5 || t(group) < t(parent) - 0.5 || bo(group) > bo(parent) + 0.5)) console.log(view, wf.workflowId, 'group', group.id, 'outside', parent.id)
        }
      }
    }
  }
})
