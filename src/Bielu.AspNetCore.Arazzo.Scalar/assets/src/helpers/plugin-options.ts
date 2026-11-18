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

/** The options; each must name a specification extension: `x-` followed by a name. */
const OPTION_KEYS = ['endLabelExtension', 'pagesExtension', 'pageExtension', 'labelExtension'] as const

/** The rule .NET (`WithArazzoOptions`) and the Aspire helper apply too, so every input path accepts the same names. */
const isSpecificationExtension = (value: unknown): value is string => typeof value === 'string' && value.startsWith('x-') && value.length > 2

/** The window global the .NET package assigns the options to. */
export const PLUGIN_OPTIONS_GLOBAL = '__BIELU_SCALAR_ARAZZO__'

/** Keeps only well-formed options; anything else (it is untyped input) is dropped. */
const sanitize = (value: unknown): ArazzoPluginOptions | undefined => {
  if (!value || typeof value !== 'object') {
    return undefined
  }
  const record = value as Record<string, unknown>
  const options: ArazzoPluginOptions = {}
  for (const key of OPTION_KEYS) {
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
    return sanitize(Object.fromEntries(OPTION_KEYS.map((key) => [key, query.get(key) ?? undefined]))) ?? {}
  } catch {
    return {}
  }
}

export const resolvePluginOptions = (explicit?: unknown, injected?: unknown, moduleUrl?: string): ArazzoPluginOptions =>
  sanitize(explicit) ?? sanitize(injected) ?? pluginOptionsFromModuleUrl(moduleUrl)
