import { setAuthState } from './auth'
import type { ConsolePluginSpec, DocumentRef } from './types'

/**
 * The structural shape of a Scalar API Reference plugin. Declared locally so this package does not
 * take a hard dependency on a specific `@scalar/types` version; Scalar validates it at runtime.
 */
export type ApiReferencePlugin = () => {
  name: string
  extensions: unknown[]
  views?: Record<string, unknown[]>
  hooks?: Record<string, unknown>
  apiClientPlugins?: unknown[]
}

/**
 * Creates a console plugin for the Scalar API Reference.
 *
 * The view's `component` is the custom element tag name (a string), so Scalar's own Vue renders a
 * plain element — the console Web Component (with its own Vue + styles) takes over from there. The
 * AsyncAPI documents (discovered from the Scalar configuration at registration time) are passed to
 * the element as the `documents` prop, so the console does not depend on Scalar binding its config.
 */
export const createConsolePlugin = (spec: ConsolePluginSpec, documents?: DocumentRef[]): ApiReferencePlugin => {
  const plugin: ApiReferencePlugin = () => ({
    name: spec.pluginName,
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
          component: spec.elementTag,
          props: documents && documents.length > 0 ? { documents } : {},
          sidebar: {
            show: true,
            label: spec.sidebarLabel,
          },
        },
      ],
    },
  })
  // Tag the factory so callers can detect an already-registered plugin without invoking it.
  ;(plugin as { pluginName?: string }).pluginName = spec.pluginName
  return plugin
}
