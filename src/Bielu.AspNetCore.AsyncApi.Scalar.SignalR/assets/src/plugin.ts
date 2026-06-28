import SignalRConsole from './components/SignalRConsole.vue'
import type { SignalRPluginConfig } from './types'

/**
 * The structural shape of a Scalar API Reference plugin. Declared locally so this package does
 * not take a hard dependency on a specific `@scalar/types` version; Scalar validates the shape
 * at runtime.
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
 * It contributes a single view into the `content.end` slot (with a sidebar entry) that renders
 * the interactive SignalR console for every hub discovered in the configured AsyncAPI documents.
 */
export const createSignalRPlugin = (config: SignalRPluginConfig): ApiReferencePlugin => () => ({
  name: 'bielu-signalr',
  extensions: [],
  views: {
    'content.end': [
      {
        component: SignalRConsole,
        props: { config },
        sidebar: {
          show: true,
          label: 'SignalR',
        },
      },
    ],
  },
})

export type { ApiReferencePlugin }
