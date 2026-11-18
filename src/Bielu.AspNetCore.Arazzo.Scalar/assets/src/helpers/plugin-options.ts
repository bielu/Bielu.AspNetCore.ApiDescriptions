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
}

/** The window global the .NET package assigns the options to. */
export const PLUGIN_OPTIONS_GLOBAL = '__BIELU_SCALAR_ARAZZO__'

/** A specification extension name: `x-` followed by a name. .NET (`WithArazzoOptions`) applies the same rule. */
const isExtensionName = (value: unknown): value is string => typeof value === 'string' && value.startsWith('x-') && value.length > 2

/** Keeps only well-formed options; anything else (it is untyped input) is dropped. */
const sanitize = (value: unknown): ArazzoPluginOptions | undefined => {
  if (!value || typeof value !== 'object') {
    return undefined
  }
  const { endLabelExtension } = value as Record<string, unknown>
  return isExtensionName(endLabelExtension) ? { endLabelExtension } : undefined
}

/** Reads the options from the plugin module's own URL; an ES module has no `document.currentScript`. */
export const pluginOptionsFromModuleUrl = (moduleUrl: string | undefined): ArazzoPluginOptions => {
  if (!moduleUrl) {
    return {}
  }
  try {
    const endLabelExtension = new URL(moduleUrl).searchParams.get('endLabelExtension')
    return isExtensionName(endLabelExtension) ? { endLabelExtension } : {}
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
