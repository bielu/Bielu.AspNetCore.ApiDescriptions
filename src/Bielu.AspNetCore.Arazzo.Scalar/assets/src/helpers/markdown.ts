import DOMPurify from 'dompurify'
import { marked } from 'marked'

/**
 * Renders a CommonMark description (the format the Arazzo spec mandates for `description` fields)
 * to sanitized HTML. Documents are untrusted input, so the output always goes through DOMPurify
 * before it reaches `v-html`.
 */
export const renderMarkdown = (value: string | undefined): string => {
  if (!value) {
    return ''
  }
  const html = marked.parse(value, { async: false, gfm: true })
  return DOMPurify.sanitize(html)
}
