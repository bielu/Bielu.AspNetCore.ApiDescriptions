import DOMPurify from 'dompurify'
import { marked } from 'marked'

/**
 * Renders a CommonMark string to sanitized HTML. Documents are untrusted input, so the output always goes through
 * DOMPurify before it reaches `v-html`.
 *
 * Exported as the subpath `@bielu/scalar-core/markdown`, not from the package root, so the console plugins that
 * import the root never pull in marked and DOMPurify.
 */
export const renderMarkdown = (value: string | null | undefined): string => {
  if (!value) {
    return ''
  }
  return DOMPurify.sanitize(marked.parse(value, { async: false, gfm: true }))
}
