import { safeName as safeFileName } from '@bielu/scalar-graph'
import type { LaidOutEdge, LaidOutNode, WorkflowLayout } from './workflow-graph'

/**
 * Exports the workflow overview as Mermaid source to paste into Markdown that renders it (GitHub, GitLab, many
 * wikis). The SVG and PNG exports are @bielu/scalar-graph's.
 */

/** `orders.placed/{id}` → `orders_placed__id_`, a name a file system and a Mermaid id both accept; `workflow` when nothing is left. */
export const safeName = (value: string): string => safeFileName(value, 'workflow')

/** Text inside a Mermaid label: quotes and the characters Mermaid reads as syntax become entity codes. */
const mermaidText = (text: string): string =>
  text.replace(/"/g, '#quot;').replace(/</g, '#lt;').replace(/>/g, '#gt;').replace(/\|/g, '#124;').replace(/\n/g, ' ')

/** A node's Mermaid shape: steps are boxes, workflows subroutines, ends stadiums; a second line (what a step does, a Pages view end's action) is kept. */
const mermaidNode = (id: string, node: LaidOutNode): string => {
  const label = mermaidText(node.subtitle ? `${node.label}<br/>${node.subtitle}` : node.label).replace(/#lt;br\/#gt;/g, '<br/>')
  switch (node.kind) {
    case 'workflow':
      return `${id}[["${label}"]]`
    case 'success':
    case 'failure':
      return `${id}(["${label}"])`
    default:
      return `${id}["${label}"]`
  }
}

/** An arrow: failure paths dashed, as in the overview; a labelled arrow keeps its label. */
const mermaidEdge = (from: string, to: string, edge: LaidOutEdge): string => {
  const arrow = edge.kind === 'failure' || edge.kind === 'return' ? '-.->' : edge.kind === 'expand' ? '-.-' : '-->'
  return edge.label ? `${from} ${arrow}|"${mermaidText(edge.label)}"| ${to}` : `${from} ${arrow} ${to}`
}

/**
 * The overview as a Mermaid flowchart, top-down: the same boxes, labels and arrows (one end node per step, as
 * drawn), an expanded workflow or a Pages view page band as a subgraph, and classes that colour success and
 * failure ends.
 */
export const toMermaid = (layout: WorkflowLayout): string => {
  const ids = new Map<string, string>()
  const used = new Set<string>()
  const idOf = (key: string): string => {
    let id = ids.get(key)
    if (!id) {
      // Node ids carry markers (`__end__:`, `::group`): collapse them so the source reads as the diagram does.
      const base = `n_${safeName(key).replace(/_+/g, '_').replace(/^_|_$/g, '')}`
      id = base
      for (let suffix = 2; used.has(id); suffix++) {
        id = `${base}_${suffix}`
      }
      used.add(id)
      ids.set(key, id)
    }
    return id
  }
  const lines = ['flowchart TD']
  const groups = layout.groups ?? []
  // Pages view: each page band becomes a subgraph of its steps, titled with the page and its route in full.
  const bandOf = new Map((layout.bands ?? []).flatMap((band) => band.nodes.map((node) => [node, band] as const)))
  const write = (group: string | undefined, indent: string) => {
    const written = new Set<string>()
    for (const node of layout.nodes.filter((candidate) => candidate.group === group)) {
      const band = bandOf.get(node.id)
      if (!band) {
        lines.push(`${indent}${mermaidNode(idOf(node.id), node)}`)
      } else if (!written.has(band.id)) {
        written.add(band.id)
        lines.push(`${indent}subgraph ${idOf(band.id)}["${mermaidText([band.title, band.route].filter(Boolean).join(' · '))}"]`)
        for (const member of layout.nodes.filter((candidate) => band.nodes.includes(candidate.id))) {
          lines.push(`${indent}  ${mermaidNode(idOf(member.id), member)}`)
        }
        lines.push(`${indent}end`)
      }
    }
    for (const child of groups.filter((candidate) => (candidate.parent ?? undefined) === group)) {
      lines.push(`${indent}subgraph ${idOf(child.id)}["${mermaidText(child.label)}"]`)
      write(child.id, `${indent}  `)
      lines.push(`${indent}end`)
    }
  }
  write(undefined, '  ')
  for (const edge of layout.edges) {
    lines.push(`  ${mermaidEdge(idOf(edge.from), idOf(edge.to), edge)}`)
  }
  const ofKind = (kind: LaidOutNode['kind']) => layout.nodes.filter((node) => node.kind === kind).map((node) => idOf(node.id))
  lines.push('  classDef success fill:#e6f4ea,stroke:#30a46c,color:#1b5e20')
  lines.push('  classDef failure fill:#fdecea,stroke:#e5484d,color:#8b1a1a')
  if (ofKind('success').length) {
    lines.push(`  class ${ofKind('success').join(',')} success`)
  }
  if (ofKind('failure').length) {
    lines.push(`  class ${ofKind('failure').join(',')} failure`)
  }
  return `${lines.join('\n')}\n`
}
