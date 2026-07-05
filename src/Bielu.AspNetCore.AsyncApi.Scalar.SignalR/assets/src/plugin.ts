import { setAuthState } from './auth'
import type { SignalRDocumentRef } from './types'

/** The custom element tag the console is rendered as (registered in `index.ts`). */
export const SIGNALR_CONSOLE_TAG = 'bielu-signalr-console'

/** The Scalar plugin's identifier, used to detect (and de-duplicate) an existing registration. */
export const SIGNALR_PLUGIN_NAME = 'bielu-signalr'

/**
 * The structural shape of a Scalar API Reference plugin. Declared locally so this package does not
 * take a hard dependency on a specific `@scalar/types` version; Scalar validates it at runtime.
 */
type ApiReferencePlugin = () => {
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
export const createSignalRPlugin = (documents?: SignalRDocumentRef[]): ApiReferencePlugin => {
  const plugin: ApiReferencePlugin = () => ({
    name: SIGNALR_PLUGIN_NAME,
    extensions: [],
    // Capture Scalar's auth state whenever the plugin is initialised or the configuration changes.
    // `auth` is only present on the custom Scalar build (feat/plugin-auth-state); on stock Scalar
    // this is a no-op because `setAuthState` guards with an `isPluginAuthState` check.
    hooks: {
      onInit: ({ auth }: { auth?: unknown }) => setAuthState(auth),
      onConfigChange: ({ auth }: { auth?: unknown }) => setAuthState(auth),
    },
    views: {
      'content.end': [
        {
          component: SIGNALR_CONSOLE_TAG,
          props: documents && documents.length > 0 ? { documents } : {},
          sidebar: {
            show: true,
            label: 'SignalR',
          },
        },
      ],
    },
  })
  // Tag the factory so callers can detect an already-registered SignalR plugin without invoking it.
  ;(plugin as { pluginName?: string }).pluginName = SIGNALR_PLUGIN_NAME
  return plugin
}

export type { ApiReferencePlugin }
