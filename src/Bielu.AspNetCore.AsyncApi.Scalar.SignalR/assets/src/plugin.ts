import { createConsolePlugin } from '@bielu/scalar-core'
import type { SignalRDocumentRef } from './types'

/** The custom element tag the console is rendered as (registered in `index.ts`). */
export const SIGNALR_CONSOLE_TAG = 'bielu-signalr-console'

/** The Scalar plugin's identifier, used to detect (and de-duplicate) an existing registration. */
export const SIGNALR_PLUGIN_NAME = 'bielu-signalr'

/** How the SignalR console registers with Scalar (plugin name, element tag, sidebar label). */
export const SIGNALR_PLUGIN_SPEC = {
  pluginName: SIGNALR_PLUGIN_NAME,
  elementTag: SIGNALR_CONSOLE_TAG,
  sidebarLabel: 'SignalR',
} as const

/**
 * The structural shape of a Scalar API Reference plugin. Declared locally (a structural copy of
 * @bielu/scalar-core's type) so the published declaration files never reference the private core
 * package; Scalar validates it at runtime.
 */
export type ApiReferencePlugin = () => {
  name: string
  extensions: unknown[]
  views?: Record<string, unknown[]>
  hooks?: Record<string, unknown>
  apiClientPlugins?: unknown[]
}

/**
 * Creates the SignalR console plugin for the Scalar API Reference.
 *
 * The view's `component` is the custom element tag name (a string), so Scalar's own Vue renders a
 * plain element — our Web Component (with its own Vue + styles) takes over from there. The AsyncAPI
 * documents (discovered from the Scalar configuration at registration time) are passed to the
 * element as the `documents` prop, so the console does not depend on Scalar binding its config.
 */
export const createSignalRPlugin = (documents?: SignalRDocumentRef[]): ApiReferencePlugin =>
  createConsolePlugin(SIGNALR_PLUGIN_SPEC, documents)
