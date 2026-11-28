/**
 * Fitting text into a diagram. SVG text does not wrap, and the widths here are estimated rather than measured,
 * so a diagram draws the same before it is on the page and in an exported file.
 */

/** About how wide a character of the diagram font is, as a share of its size. */
export const CHARACTER_WIDTH = 0.56

/** `text` cut to at most `length` characters, its last one an ellipsis when anything was cut. */
export const truncate = (text: string, length: number): string => (text.length > length ? `${text.slice(0, length - 1)}…` : text)

/**
 * Wraps `text` into lines at most `width` wide at `fontSize`, breaking between words (a word longer than a line is
 * broken itself), and keeps at most `maxLines`, ending the last with an ellipsis when text is left over.
 */
export const wrapText = (text: string, width: number, fontSize: number, maxLines = Number.POSITIVE_INFINITY): string[] => {
  const perLine = Math.max(1, Math.floor(width / (fontSize * CHARACTER_WIDTH)))
  const words = text.split(/\s+/).filter(Boolean)
  const lines: string[] = []
  let current = ''
  for (const word of words) {
    let rest = word
    while (rest.length > perLine) {
      if (current) {
        lines.push(current)
        current = ''
      }
      lines.push(rest.slice(0, perLine))
      rest = rest.slice(perLine)
    }
    if (!current) {
      current = rest
    } else if (current.length + 1 + rest.length <= perLine) {
      current = `${current} ${rest}`
    } else {
      lines.push(current)
      current = rest
    }
  }
  if (current) {
    lines.push(current)
  }
  if (lines.length <= maxLines) {
    return lines
  }
  const kept = lines.slice(0, maxLines)
  const last = kept[maxLines - 1]!
  kept[maxLines - 1] = `${last.length >= perLine ? last.slice(0, Math.max(0, perLine - 1)) : last}…`
  return kept
}
