import { defineCustomElement } from 'vue'
import { resolveDocuments } from './discovery'
import { createConsolePlugin } from './plugin'
import type { ConsoleBundleSpec } from './types'

/**
 * Inject the component's compiled (scoped) styles into the document head.
 *
 * With `shadowRoot: false` the console renders in the light DOM, but Vue does not reliably inject the
 * collected styles there — so we do it ourselves. The styles are scoped (`[data-v-*]`), so they only
 * match the console's own markup and never leak into Scalar's page.
 */
function injectStyles(spec: ConsoleBundleSpec, component: unknown): void {
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
export function registerConsoleElement(spec: ConsoleBundleSpec, component: any): void {
  if (typeof customElements === 'undefined' || customElements.get(spec.elementTag)) {
    return
  }
  injectStyles(spec, component)
  customElements.define(spec.elementTag, defineCustomElement(component, { shadowRoot: false }))
}

/**
 * Adds the console plugin to a Scalar configuration without mutating the caller's object. The
 * AsyncAPI documents are discovered from the very config Scalar is about to render, then handed to
 * the plugin so the console never has to rely on Scalar re-binding its config to the element.
 */
function withConsolePlugin(spec: ConsoleBundleSpec, config: Record<string, any>): Record<string, any> {
  const plugins = Array.isArray(config.plugins) ? config.plugins.slice() : []
  // Don't register a second time if the consumer already added the plugin (e.g. they both load the
  // auto-registering bundle and call the plugin factory themselves).
  if (plugins.some((plugin) => (plugin as { pluginName?: string })?.pluginName === spec.pluginName)) {
    return config
  }
  const documents = resolveDocuments(config, spec.discovery)
  plugins.push(createConsolePlugin(spec, documents))
  return { ...config, plugins }
}

/** Wrap `Scalar.createApiReference` so every call registers the console plugin. */
function wrapScalar(spec: ConsoleBundleSpec, scalar: any): any {
  if (scalar && typeof scalar.createApiReference === 'function' && !scalar[spec.wrappedFlag]) {
    const original = scalar.createApiReference.bind(scalar)
    scalar.createApiReference = (element: unknown, config: Record<string, any> = {}) =>
      original(element, withConsolePlugin(spec, config))
    scalar[spec.wrappedFlag] = true
  }
  return scalar
}

/**
 * Install the hook regardless of script order: if Scalar's bundle has already set `window.Scalar`
 * we wrap it now; otherwise we intercept the assignment so we wrap it the moment Scalar registers.
 *
 * Several console bundles can coexist on one page: each wraps with its own `wrappedFlag`, and the
 * property interception below re-defines the (configurable) accessor installed by earlier bundles,
 * chaining the wraps.
 */
export function installScalarHook(spec: ConsoleBundleSpec): void {
  if (typeof window === 'undefined') {
    return
  }
  let stored: any = (window as any).Scalar
  if (stored) {
    wrapScalar(spec, stored)
  }
  try {
    Object.defineProperty(window, 'Scalar', {
      configurable: true,
      enumerable: true,
      get: () => stored,
      set: (value) => {
        stored = wrapScalar(spec, value)
      },
    })
  } catch {
    // `Scalar` is already defined and non-configurable — best-effort wrap what is there.
    wrapScalar(spec, (window as any).Scalar)
  }
}

/** Register the console element and hook Scalar — the whole side-effect entry of a console bundle. */
export function bootstrapConsole(spec: ConsoleBundleSpec, component: any): void {
  registerConsoleElement(spec, component)
  installScalarHook(spec)
}
