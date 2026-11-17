import { defineCustomElement } from 'vue'
import { resolveDocuments } from './discovery'
import { createConsolePlugin, type ApiReferencePlugin } from './plugin'
import type { ConsoleBundleSpec, ConsoleModuleSpec } from './types'

/**
 * Inject the component's compiled (scoped) styles into the document head.
 *
 * With `shadowRoot: false` the console renders in the light DOM, but Vue does not reliably inject the
 * collected styles there — so we do it ourselves. The styles are scoped (`[data-v-*]`), so they only
 * match the console's own markup and never leak into Scalar's page.
 */
function injectStyles(spec: ConsoleModuleSpec, component: unknown): void {
  if (typeof document === 'undefined' || document.getElementById(spec.stylesId)) {
    return
  }
  const styles = (component as { styles?: string[] }).styles
  if (!styles || styles.length === 0) {
    return
  }
  const style = document.createElement('style')
  style.id = spec.stylesId
  style.textContent = styles.join('\n')
  document.head.appendChild(style)
}

/**
 * Register the console as a Web Component (idempotent).
 *
 * `shadowRoot: false` (Vue 3.5+) renders into the light DOM so Scalar's own stylesheet and design
 * tokens apply, letting the console match the look of Scalar's OpenAPI operations.
 */
export function registerConsoleElement(spec: ConsoleModuleSpec, component: any): void {
  if (typeof customElements === 'undefined' || customElements.get(spec.elementTag)) {
    return
  }
  injectStyles(spec, component)
  customElements.define(spec.elementTag, defineCustomElement(component, { shadowRoot: false }))
}

/** Identifies one plugin installed by a page-level hook (see `installScalarPluginHook`). */
export type ScalarPluginHookSpec = {
  /** The Scalar plugin's identifier, used to detect (and de-duplicate) an existing registration. */
  pluginName: string
  /** The marker property set on `window.Scalar` once wrapped (e.g. `'__bieluSignalRWrapped'`). */
  wrappedFlag: string
}

/** Builds the plugin from the configuration Scalar is about to render. */
export type ScalarPluginFactory = (config: Record<string, any>) => ApiReferencePlugin

/**
 * Adds a plugin to a Scalar configuration without mutating the caller's object. The factory gets
 * the very config Scalar is about to render, so a plugin can derive its inputs (e.g. the AsyncAPI
 * documents a console drives) from it instead of relying on Scalar re-binding its config later.
 */
function withPlugin(
  spec: ScalarPluginHookSpec,
  createPlugin: ScalarPluginFactory,
  config: Record<string, any>,
): Record<string, any> {
  const plugins = Array.isArray(config.plugins) ? config.plugins.slice() : []
  // Don't register a second time if the consumer already added the plugin (e.g. they both load the
  // auto-registering bundle and call the plugin factory themselves).
  if (plugins.some((plugin) => (plugin as { pluginName?: string })?.pluginName === spec.pluginName)) {
    return config
  }
  plugins.push(createPlugin(config))
  return { ...config, plugins }
}

/** Wrap `Scalar.createApiReference` so every call registers the plugin. */
function wrapScalar(spec: ScalarPluginHookSpec, createPlugin: ScalarPluginFactory, scalar: any): any {
  if (scalar && typeof scalar.createApiReference === 'function' && !scalar[spec.wrappedFlag]) {
    const original = scalar.createApiReference.bind(scalar)
    scalar.createApiReference = (element: unknown, config: Record<string, any> = {}) =>
      original(element, withPlugin(spec, createPlugin, config))
    scalar[spec.wrappedFlag] = true
  }
  return scalar
}

/**
 * Adds a plugin to every Scalar API Reference on the page, regardless of script order: if Scalar's
 * bundle has already set `window.Scalar` we wrap it now; otherwise we intercept the assignment so we
 * wrap it the moment Scalar registers.
 *
 * Several bundles can coexist on one page: each wraps with its own `wrappedFlag`, and the property
 * interception below re-defines the (configurable) accessor installed by earlier bundles, chaining
 * the wraps.
 */
export function installScalarPluginHook(spec: ScalarPluginHookSpec, createPlugin: ScalarPluginFactory): void {
  if (typeof window === 'undefined') {
    return
  }
  // An earlier bundle may have installed its own accessor. Capture its setter so we can chain onto
  // it instead of replacing it — otherwise only the last-installed bundle would wrap Scalar when it
  // is finally assigned.
  const priorSetter = Object.getOwnPropertyDescriptor(window, 'Scalar')?.set
  let stored: any = (window as any).Scalar
  if (stored) {
    wrapScalar(spec, createPlugin, stored)
  }
  try {
    Object.defineProperty(window, 'Scalar', {
      configurable: true,
      enumerable: true,
      get: () => stored,
      set: (value) => {
        // Let the earlier hook wrap first (it mutates the same Scalar object in place), then apply
        // our own wrap, so every installed bundle's plugin is chained onto the loaded Scalar.
        priorSetter?.call(window, value)
        stored = wrapScalar(spec, createPlugin, value)
      },
    })
  } catch {
    // `Scalar` is already defined and non-configurable — best-effort wrap what is there.
    wrapScalar(spec, createPlugin, (window as any).Scalar)
  }
}

/**
 * Installs the console plugin hook. The AsyncAPI documents are discovered from the config being
 * rendered and handed to the console plugin.
 */
export function installScalarHook(spec: ConsoleBundleSpec): void {
  installScalarPluginHook(spec, (config) => createConsolePlugin(spec, resolveDocuments(config, spec.discovery)))
}

/** Register the console element and hook Scalar — the whole side-effect entry of a console bundle. */
export function bootstrapConsole(spec: ConsoleBundleSpec, component: any): void {
  registerConsoleElement(spec, component)
  installScalarHook(spec)
}
