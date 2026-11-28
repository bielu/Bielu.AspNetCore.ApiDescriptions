/**
 * Exports a rendered diagram for use outside Scalar: as a standalone SVG or PNG (docs, ADRs, slides).
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
 * A standalone copy of a rendered diagram: every element's computed presentation written onto it (the live
 * diagram is styled by the page's CSS and Scalar's theme variables, which a file doesn't have), at the graph's
 * natural size, on the page's background colour.
 */
export const standaloneSvg = (svg: SVGSVGElement, size: { width: number; height: number }): string => {
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
  const width = Math.max(size.width, 1)
  const height = Math.max(size.height, 1)
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

/**
 * `orders.placed/{id}` → `orders_placed__id_`, a name a file system and a Mermaid id both accept; `fallback` when
 * nothing of `value` is left.
 */
export const safeName = (value: string, fallback = 'diagram'): string => value.replace(/[^A-Za-z0-9_-]+/g, '_') || fallback
