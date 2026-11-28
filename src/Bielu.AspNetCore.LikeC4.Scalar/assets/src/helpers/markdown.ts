import { renderMarkdown as renderCommonMark } from '@bielu/scalar-core/markdown'
import type { LikeC4Text } from '../types'

/**
 * Renders a LikeC4 text (a `description` or `notes`: Markdown, or plain text) to sanitized HTML. The model
 * is untrusted input: Markdown goes through DOMPurify before it reaches `v-html`, and plain text is escaped
 * whole, so it holds no markup to sanitize.
 */
export const renderMarkdown = (value: LikeC4Text | null | undefined): string => {
  if (!value) {
    return ''
  }
  if (typeof value === 'object' && typeof value.md !== 'string') {
    return escapeHtml(value.txt ?? '').replace(/\n/g, '<br>')
  }
  return renderCommonMark(typeof value === 'string' ? value : value.md!)
}

/**
 * A LikeC4 text as one line of plain text, for a diagram label or a tooltip: Markdown marks dropped from
 * Markdown, while plain text (`txt`) keeps every character and only has its whitespace collapsed.
 */
export const plainText = (value: LikeC4Text | null | undefined): string => {
  if (!value) {
    return ''
  }
  if (typeof value === 'object' && typeof value.md !== 'string') {
    return (value.txt ?? '').replace(/\s+/g, ' ').trim()
  }
  const text = typeof value === 'string' ? value : value.md!
  return text
    .replace(/!\[([^\]]*)\]\([^)]*\)/g, '$1')
    .replace(/\[([^\]]*)\]\([^)]*\)/g, '$1')
    .replace(/[*_`#>]/g, '')
    .replace(/\s+/g, ' ')
    .trim()
}

const escapeHtml = (value: string): string =>
  value.replace(/&/g, '&amp;').replace(/</g, '&lt;').replace(/>/g, '&gt;').replace(/"/g, '&quot;')
