import type { StepTarget } from './resolve-target'

/** The part of a Scalar sidebar entry (`TraversedEntry` in `@scalar/workspace-store`) a step target is matched on. */
type NavigationEntry = {
  id?: unknown
  type?: unknown
  method?: unknown
  path?: unknown
  operationName?: unknown
  channelName?: unknown
  children?: unknown
}

const isEntry = (value: unknown): value is NavigationEntry => !!value && typeof value === 'object'

/** Depth-first search of a sidebar tree; the first entry that matches wins, as in document order. */
const findEntry = (node: unknown, matches: (entry: NavigationEntry) => boolean, depth = 0): NavigationEntry | undefined => {
  if (!isEntry(node) || depth > 32) {
    return undefined
  }
  if (matches(node)) {
    return node
  }
  for (const child of Array.isArray(node.children) ? node.children : []) {
    const found = findEntry(child, matches, depth + 1)
    if (found) {
      return found
    }
  }
  return undefined
}

/**
 * The id of the sidebar entry a resolved step target has in its own document, so a link can open it.
 *
 * Scalar stores each loaded document's sidebar tree on `x-scalar-navigation`. Reading the ids from
 * there, rather than rebuilding them, keeps the link right under Scalar's own slug rules, tags and
 * any `generateOperationSlug` the page configures. `undefined` when the target is not resolved or
 * the document has no such entry (for example an operation hidden from the sidebar).
 */
export const targetNavigationId = (
  target: StepTarget | undefined,
  documents: Record<string, unknown>,
): string | undefined => {
  if (!target || !target.documentName) {
    return undefined
  }
  const root = (documents[target.documentName] as { 'x-scalar-navigation'?: unknown } | undefined)?.['x-scalar-navigation']

  let entry: NavigationEntry | undefined
  if (target.kind === 'workflow') {
    // A workflow of another Arazzo document: its entry is the one `traverseArazzoDocument` gave it there.
    const workflowId = target.workflow?.workflowId
    entry = workflowId ? findEntry(root, (candidate) => typeof candidate.id === 'string' && candidate.id.endsWith(`/workflow/${workflowId}`)) : undefined
  } else if (target.kind === 'operation') {
    const method = target.method?.toLowerCase()
    entry = findEntry(
      root,
      (candidate) =>
        candidate.type === 'operation' &&
        typeof candidate.method === 'string' &&
        candidate.method.toLowerCase() === method &&
        candidate.path === target.path,
    )
  } else {
    // An operation target opens the operation itself; a channel target, the channel.
    entry = target.operationName
      ? findEntry(root, (candidate) => candidate.type === 'asyncapi-operation' && candidate.operationName === target.operationName)
      : undefined
    entry ??= target.channelName
      ? findEntry(root, (candidate) => candidate.type === 'asyncapi-channel' && candidate.channelName === target.channelName)
      : undefined
  }
  return typeof entry?.id === 'string' ? entry.id : undefined
}

/**
 * The id of the sidebar entry Scalar gives a named schema of a loaded document (`#/components/schemas/Order`,
 * listed under Models), so a link can open the schema's page. `undefined` when the document lists no such model.
 */
export const schemaNavigationId = (documentName: string, ref: string, documents: Record<string, unknown>): string | undefined => {
  const root = (documents[documentName] as { 'x-scalar-navigation'?: unknown } | undefined)?.['x-scalar-navigation']
  const entry = findEntry(root, (candidate) => candidate.type === 'model' && (candidate as { ref?: unknown }).ref === ref)
  return typeof entry?.id === 'string' ? entry.id : undefined
}

/**
 * The page URL that opens a sidebar entry: this page with the entry's id as the hash. That is the
 * deep-link form Scalar reads on load to pick the document and scroll to the entry.
 */
export const navigationHref = (id: string, location: Pick<Location, 'pathname' | 'search'>): string =>
  `${location.pathname}${location.search}#${id}`

/** The document a Scalar deep link names: the first segment of its hash (`events` in `#events/channel/x`). */
export const documentSlugOf = (hash: string): string | undefined => {
  let decoded = hash.replace(/^#/, '')
  try {
    decoded = decodeURIComponent(decoded)
  } catch {
    // Not percent-encoded after all: read it as written.
  }
  return decoded.split('/')[0] || undefined
}

type HistoryWindow = Pick<Window, 'addEventListener'> & {
  history: Pick<History, 'pushState' | 'replaceState'>
  location: Pick<Location, 'hash' | 'reload'>
  __bieluArazzoHistory?: boolean
}

/**
 * Reloads when Back or Forward lands on a URL of another document than the one shown. Scalar picks
 * the document from the URL only on load; on `popstate` it scrolls within the document it shows, so
 * Back after a cross-document link would change the URL and stay put.
 *
 * The shown document is tracked from the URL: at load, and whenever Scalar rewrites the URL (it calls
 * `replaceState` as the reader scrolls or switches documents). History state cannot carry this,
 * since Scalar replaces it with `{}`. Installed once per page.
 */
export const installCrossDocumentHistory = (target: HistoryWindow = window as unknown as HistoryWindow): void => {
  if (target.__bieluArazzoHistory) {
    return
  }
  target.__bieluArazzoHistory = true

  let shown = documentSlugOf(target.location.hash)
  const track = (method: 'pushState' | 'replaceState') => {
    const original = target.history[method].bind(target.history)
    target.history[method] = (...args: Parameters<History['pushState']>) => {
      original(...args)
      shown = documentSlugOf(target.location.hash) ?? shown
    }
  }
  track('pushState')
  track('replaceState')

  target.addEventListener('popstate', () => {
    const next = documentSlugOf(target.location.hash)
    if (next && shown && next !== shown) {
      target.location.reload()
      return
    }
    shown = next ?? shown
  })
}

/**
 * Opens a link to an entry of another document. Scalar picks the active document from the URL only
 * when the page loads, so changing the hash alone would scroll nowhere: the new URL is pushed and the
 * page reloaded. A click with a modifier key (new tab or window) is left to the browser.
 */
export const openInOtherDocument = (event: MouseEvent, href: string): void => {
  if (event.defaultPrevented || event.button !== 0 || event.metaKey || event.ctrlKey || event.shiftKey || event.altKey) {
    return
  }
  event.preventDefault()
  window.history.pushState(null, '', href)
  window.location.reload()
}

/**
 * A short name for what a step targets, instead of the raw reference: the operationId of an OpenAPI
 * operation, the AsyncAPI operation or channel name. Empty for a resolved operation without an
 * operationId, which its `GET /path` signature already names. An unresolved target keeps its raw
 * reference, since that is all there is to show.
 */
export const targetDisplayName = (target: StepTarget): string => {
  if (target.kind === 'operation' && target.documentName) {
    const operationId = target.operation?.operationId
    // Reached through `operationPath`, an operation without an operationId is named by its signature alone.
    return typeof operationId === 'string' && operationId ? operationId : ''
  }
  if (target.kind === 'channel' && target.documentName) {
    return target.operationName ?? target.channelName ?? target.identifier
  }
  return target.identifier
}
