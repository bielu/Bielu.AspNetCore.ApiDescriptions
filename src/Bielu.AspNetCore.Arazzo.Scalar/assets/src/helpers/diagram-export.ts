import type { LaidOutEdge, LaidOutNode, WorkflowLayout } from './workflow-graph'

/**
 * Exports the workflow overview for use outside Scalar: as a standalone SVG or PNG (docs, ADRs, slides), or as
 * Mermaid source to paste into Markdown that renders it (GitHub, GitLab, many wikis).
 */

/** The presentation properties copied onto each exported element, so it looks the same without the page's CSS. */
const STYLE_PROPERTIES = [
  'fill',
  'fill-opacity',
  'stroke',
  'stroke-width',
  'stroke-dasharray',
  'stroke-opacity',
  'opacity',
  'font-family',
  'font-size',
  'font-weight',
  'font-style',
  'text-anchor',
  'dominant-baseline',
  'visibility',
  'display',
] as const

/**
 * A standalone copy of a rendered overview: every element's computed presentation written onto it (the live
 * diagram is styled by the page's CSS and Scalar's theme variables, which a file doesn't have), at the graph's
 * natural size, on the page's background colour.
 */
export const standaloneSvg = (svg: SVGSVGElement, layout: WorkflowLayout): string => {
  const copy = svg.cloneNode(true) as SVGSVGElement
  const live = [svg, ...Array.from(svg.querySelectorAll('*'))]
  const copies = [copy, ...Array.from(copy.querySelectorAll('*'))]
  live.forEach((element, index) => {
    const target = copies[index] as SVGElement | undefined
    if (!target || typeof getComputedStyle === 'undefined') {
      return
    }
    const computed = getComputedStyle(element)
    const style = STYLE_PROPERTIES.map((property) => [property, computed.getPropertyValue(property)] as const)
      .filter(([, value]) => value && value !== 'normal' && value !== 'auto')
      .map(([property, value]) => `${property}:${value}`)
      .join(';')
    if (style) {
      target.setAttribute('style', style)
    }
    target.removeAttribute('class')
    target.removeAttribute('tabindex')
    target.removeAttribute('role')
  })
  const width = Math.max(layout.width, 1)
  const height = Math.max(layout.height, 1)
  copy.setAttribute('xmlns', 'http://www.w3.org/2000/svg')
  copy.setAttribute('width', String(width))
  copy.setAttribute('height', String(height))
  copy.setAttribute('viewBox', `0 0 ${width} ${height}`)
  const background = backgroundOf(svg)
  if (background) {
    const rect = copy.ownerDocument.createElementNS('http://www.w3.org/2000/svg', 'rect')
    rect.setAttribute('width', '100%')
    rect.setAttribute('height', '100%')
    rect.setAttribute('fill', background)
    copy.insertBefore(rect, copy.firstChild)
  }
  return `<?xml version="1.0" encoding="UTF-8"?>\n${new XMLSerializer().serializeToString(copy)}`
}

/** The first opaque background behind an element, so the export keeps the light or dark theme it was drawn in. */
const backgroundOf = (element: Element): string | undefined => {
  for (let current: Element | null = element; current; current = current.parentElement) {
    const color = typeof getComputedStyle === 'undefined' ? '' : getComputedStyle(current).backgroundColor
    if (color && color !== 'transparent' && !/rgba\(.*,\s*0\)$/.test(color)) {
      return color
    }
  }
  return undefined
}

/** Renders an SVG document onto a canvas at `scale` (2 for a sharp image on high-density screens), as a PNG. */
export const svgToPng = (svg: string, width: number, height: number, scale = 2): Promise<Blob> =>
  new Promise((resolve, reject) => {
    const image = new Image()
    const url = URL.createObjectURL(new Blob([svg], { type: 'image/svg+xml' }))
    image.onload = () => {
      const canvas = document.createElement('canvas')
      canvas.width = Math.ceil(width * scale)
      canvas.height = Math.ceil(height * scale)
      const context = canvas.getContext('2d')
      if (!context) {
        URL.revokeObjectURL(url)
        reject(new Error('No 2D canvas context'))
        return
      }
      context.scale(scale, scale)
      context.drawImage(image, 0, 0, width, height)
      URL.revokeObjectURL(url)
      canvas.toBlob((blob) => (blob ? resolve(blob) : reject(new Error('The canvas produced no image'))), 'image/png')
    }
    image.onerror = () => {
      URL.revokeObjectURL(url)
      reject(new Error('The diagram could not be drawn as an image'))
    }
    image.src = url
  })

/** Saves a blob as a file through a temporary link. */
export const downloadBlob = (blob: Blob, fileName: string): void => {
  const url = URL.createObjectURL(blob)
  const link = document.createElement('a')
  link.href = url
  link.download = fileName
  link.style.display = 'none'
  document.body.appendChild(link)
  link.click()
  link.remove()
  setTimeout(() => URL.revokeObjectURL(url), 0)
}

/** `orders.placed/{id}` → `orders_placed__id_`, a name a file system and a Mermaid id both accept. */
export const safeName = (value: string): string => value.replace(/[^A-Za-z0-9_-]+/g, '_') || 'workflow'

/** Text inside a Mermaid label: quotes and the characters Mermaid reads as syntax become entity codes. */
const mermaidText = (text: string): string =>
  text.replace(/"/g, '#quot;').replace(/</g, '#lt;').replace(/>/g, '#gt;').replace(/\|/g, '#124;').replace(/\n/g, ' ')

/** A node's Mermaid shape: steps are boxes, workflows subroutines, ends stadiums. */
const mermaidNode = (id: string, node: LaidOutNode): string => {
  const label = mermaidText(node.subtitle && node.kind === 'step' ? `${node.label}<br/>${node.subtitle}` : node.label).replace(/#lt;br\/#gt;/g, '<br/>')
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
