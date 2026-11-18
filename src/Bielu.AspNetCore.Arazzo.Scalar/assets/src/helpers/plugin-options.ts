/**
 * Options of the Arazzo plugin. They reach it in one of three ways, the first one present winning:
 * `createArazzoPlugin(options)` (npm), the `window.__BIELU_SCALAR_ARAZZO__` global the .NET package writes
 * (`WithArazzoOptions(o => o.EndLabelExtension = …)`), or the query string of the `scalar-plugin.mjs` URL
 * (`?endLabelExtension=x-page`, for Scalar's `pluginUrls`, where neither of the others exists).
 */
export type ArazzoPluginOptions = {
  /**
   * A specification extension (such as `x-page`) on `end` failure actions whose value names the
   * end node those actions lead to in the workflow overview, in place of the action's name.
   */
  endLabelExtension?: string
  /** The Pages view: a document extension holding the map of page id → `{ title, route, description }`. */
  pagesExtension?: string
  /** The Pages view: the extension naming the page of a step (the user is on it) or of an action (they land on it). */
  pageExtension?: string
  /** The Pages view: the extension holding a plain-language label of a workflow, step or action. */
  labelExtension?: string
}

/** The Pages view's options; each must name a specification extension (`x-…`). */
const PAGES_KEYS = ['pagesExtension', 'pageExtension', 'labelExtension'] as const

const isSpecificationExtension = (value: unknown): value is string => typeof value === 'string' && value.startsWith('x-') && value.length > 2

/** The window global the .NET package assigns the options to. */
export const PLUGIN_OPTIONS_GLOBAL = '__BIELU_SCALAR_ARAZZO__'

/** A specification extension name: `x-` followed by a name. .NET (`WithArazzoOptions`) applies the same rule. */
const isExtensionName = (value: unknown): value is string => typeof value === 'string' && value.startsWith('x-') && value.length > 2

/** Keeps only well-formed options; anything else (it is untyped input) is dropped. */
const sanitize = (value: unknown): ArazzoPluginOptions | undefined => {
  if (!value || typeof value !== 'object') {
    return undefined
  }
  const record = value as Record<string, unknown>
  const options: ArazzoPluginOptions = {}
  if (isExtensionName(record.endLabelExtension)) {
    options.endLabelExtension = record.endLabelExtension
  }
  for (const key of PAGES_KEYS) {
    if (isSpecificationExtension(record[key])) {
      options[key] = record[key]
    }
  }
  return Object.keys(options).length ? options : undefined
}

/** Reads the options from the plugin module's own URL; an ES module has no `document.currentScript`. */
export const pluginOptionsFromModuleUrl = (moduleUrl: string | undefined): ArazzoPluginOptions => {
  if (!moduleUrl) {
    return {}
  }
  try {
    const query = new URL(moduleUrl).searchParams
    return sanitize(Object.fromEntries(['endLabelExtension', ...PAGES_KEYS].map((key) => [key, query.get(key) ?? undefined]))) ?? {}
  } catch {
    return {}
  }
}

export const resolvePluginOptions = (explicit?: unknown, injected?: unknown, moduleUrl?: string): ArazzoPluginOptions =>
  sanitize(explicit) ?? sanitize(injected) ?? pluginOptionsFromModuleUrl(moduleUrl)

let current: ArazzoPluginOptions = {}

/** The options the entry point resolved, read by the document view when it draws the overview. */
export const pluginOptions = (): ArazzoPluginOptions => current

export const setPluginOptions = (options: ArazzoPluginOptions): void => {
  current = options
}
