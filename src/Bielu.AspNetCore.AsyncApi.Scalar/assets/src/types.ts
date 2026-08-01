/** A reference to an AsyncAPI document — either by URL or as an already-parsed object. */
export type DocumentRef = {
  /** Logical document name (matches the AsyncAPI/source title). */
  name: string
  /** URL the AsyncAPI JSON document is served from. */
  url?: string
  /** An inline, already-parsed document object (from a Scalar source's `content`). */
  doc?: Record<string, any>
}

/** A minimal representation of an AsyncAPI security scheme, extracted at document-parse time. */
export type SecuritySchemeModel = {
  /** e.g. `'apiKey'`, `'http'`, `'oauth2'`, `'openIdConnect'`. */
  type: string
  /** For `apiKey`: `'query'`, `'header'`, or `'cookie'`. */
  in?: string
  /** For `apiKey`: the query-param or header field name. */
  name?: string
  /** For `http`: `'bearer'`, `'basic'`, etc. */
  scheme?: string
}

/** Identity of one console plugin: how it registers with Scalar and renders on the page. */
export type ConsolePluginSpec = {
  /** The Scalar plugin's identifier, used to detect (and de-duplicate) an existing registration. */
  pluginName: string
  /** The custom element tag the console is rendered as. */
  elementTag: string
  /** The label shown in Scalar's sidebar. */
  sidebarLabel: string
}

/** How a console bundle discovers its AsyncAPI documents. */
export type DiscoveryOptions = {
  /** The key looked up on the Scalar config for an inline plugin config (e.g. `'signalr'`). */
  configKey: string
  /** The window global the .NET package assigns overrides to (e.g. `'__BIELU_SCALAR_SIGNALR__'`). */
  globalName: string
}

/**
 * A console rendered as a Web Component: plugin identity plus its style-injection id.
 *
 * This is everything the `pluginUrls` entry point needs — there Scalar registers the plugin itself,
 * so none of `ConsoleBundleSpec`'s page-level self-installation wiring applies.
 */
export type ConsoleModuleSpec = ConsolePluginSpec & {
  /** The id of the `<style>` element the console's scoped styles are injected under. */
  stylesId: string
}

/** A console bundle: everything a module needs, plus the page-level wiring it needs to self-install. */
export type ConsoleBundleSpec = ConsoleModuleSpec & {
  discovery: DiscoveryOptions
  /** The marker property set on `window.Scalar` once wrapped (e.g. `'__bieluSignalRWrapped'`). */
  wrappedFlag: string
}
