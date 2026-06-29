import { defineCustomElement } from 'vue'
import SignalRConsole from './components/SignalRConsole.vue'
import { resolveDocuments } from './discovery'
import { createSignalRPlugin, SIGNALR_CONSOLE_TAG, SIGNALR_PLUGIN_NAME } from './plugin'

export { createSignalRPlugin } from './plugin'
export { loadSignalRHubs, parseSignalRHubs } from './signalr-bindings'
export type {
  SignalRDirection,
  SignalRDocumentRef,
  SignalRHubModel,
  SignalROperationModel,
  SignalRPluginConfig,
} from './types'

/**
 * Inject the component's compiled (scoped) styles into the document head.
 *
 * With `shadowRoot: false` the console renders in the light DOM, but Vue does not reliably inject the
 * collected styles there — so we do it ourselves. The styles are scoped (`[data-v-*]`), so they only
 * match the console's own markup and never leak into Scalar's page.
 */
function injectStyles(): void {
  if (typeof document === 'undefined' || document.getElementById('bielu-signalr-styles')) {
    return
  }
  const styles = (SignalRConsole as unknown as { styles?: string[] }).styles
  if (!styles || styles.length === 0) {
    return
  }
  const style = document.createElement('style')
  style.id = 'bielu-signalr-styles'
  style.textContent = styles.join('\n')
  document.head.appendChild(style)
}

/**
 * Register the console as a Web Component (idempotent).
 *
 * `shadowRoot: false` (Vue 3.5+) renders into the light DOM so Scalar's own stylesheet and design
 * tokens apply, letting the console match the look of Scalar's OpenAPI operations.
 */
function registerElement(): void {
  if (typeof customElements === 'undefined' || customElements.get(SIGNALR_CONSOLE_TAG)) {
    return
  }
  injectStyles()
  customElements.define(SIGNALR_CONSOLE_TAG, defineCustomElement(SignalRConsole, { shadowRoot: false }))
}

/**
 * Adds the SignalR plugin to a Scalar configuration without mutating the caller's object. The
 * AsyncAPI documents are discovered from the very config Scalar is about to render, then handed to
 * the plugin so the console never has to rely on Scalar re-binding its config to the element.
 */
function withSignalRPlugin(config: Record<string, any>): Record<string, any> {
  const plugins = Array.isArray(config.plugins) ? config.plugins.slice() : []
  // Don't register a second time if the consumer already added the plugin (e.g. they both load the
  // auto-registering bundle and call `createSignalRPlugin()` themselves).
  if (plugins.some((plugin) => (plugin as { pluginName?: string })?.pluginName === SIGNALR_PLUGIN_NAME)) {
    return config
  }
  const documents = resolveDocuments(config)
  plugins.push(createSignalRPlugin(documents))
  return { ...config, plugins }
}

/** Wrap `Scalar.createApiReference` so every call registers the SignalR plugin. */
function wrapScalar(scalar: any): any {
  if (scalar && typeof scalar.createApiReference === 'function' && !scalar.__bieluSignalRWrapped) {
    const original = scalar.createApiReference.bind(scalar)
    scalar.createApiReference = (element: unknown, config: Record<string, any> = {}) =>
      original(element, withSignalRPlugin(config))
    scalar.__bieluSignalRWrapped = true
  }
  return scalar
}

/**
 * Install the hook regardless of script order: if Scalar's bundle has already set `window.Scalar`
 * we wrap it now; otherwise we intercept the assignment so we wrap it the moment Scalar registers.
 */
function installScalarHook(): void {
  if (typeof window === 'undefined') {
    return
  }
  let stored: any = (window as any).Scalar
  if (stored) {
    wrapScalar(stored)
  }
  try {
    Object.defineProperty(window, 'Scalar', {
      configurable: true,
      enumerable: true,
      get: () => stored,
      set: (value) => {
        stored = wrapScalar(value)
      },
    })
  } catch {
    // `Scalar` is already defined and non-configurable — best-effort wrap what is there.
    wrapScalar((window as any).Scalar)
  }
}

registerElement()
installScalarHook()
