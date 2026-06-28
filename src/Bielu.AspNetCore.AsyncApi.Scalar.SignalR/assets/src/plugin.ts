/** The custom element tag the console is rendered as (registered in `index.ts`). */
export const SIGNALR_CONSOLE_TAG = 'bielu-signalr-console'

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
 * plain element — our Web Component (with its own Vue + styles) takes over from there. Scalar binds
 * its configuration to the element as the `options` property, which the console uses to discover the
 * AsyncAPI documents to scan for SignalR hubs.
 */
export const createSignalRPlugin = (): ApiReferencePlugin => () => ({
  name: 'bielu-signalr',
  extensions: [],
  views: {
    'content.end': [
      {
        component: SIGNALR_CONSOLE_TAG,
        sidebar: {
          show: true,
          label: 'SignalR',
        },
      },
    ],
  },
})

export type { ApiReferencePlugin }
