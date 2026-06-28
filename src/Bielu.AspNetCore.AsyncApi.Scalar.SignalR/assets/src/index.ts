import { defineCustomElement } from 'vue'
import SignalRConsole from './components/SignalRConsole.vue'
import { createSignalRPlugin, SIGNALR_CONSOLE_TAG } from './plugin'

export { createSignalRPlugin } from './plugin'
export { loadSignalRHubs, parseSignalRHubs } from './signalr-bindings'
export type {
  SignalRDirection,
  SignalRDocumentRef,
  SignalRHubModel,
  SignalROperationModel,
  SignalRPluginConfig,
} from './types'

/** Register the console as a Web Component (idempotent). */
function registerElement(): void {
  if (typeof customElements === 'undefined' || customElements.get(SIGNALR_CONSOLE_TAG)) {
    return
  }
  customElements.define(SIGNALR_CONSOLE_TAG, defineCustomElement(SignalRConsole))
}

/** Adds the SignalR plugin to a Scalar configuration without mutating the caller's object. */
function withSignalRPlugin(config: Record<string, any>): Record<string, any> {
  const plugins = Array.isArray(config.plugins) ? config.plugins.slice() : []
  plugins.push(createSignalRPlugin())
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
