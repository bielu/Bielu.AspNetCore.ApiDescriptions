import { bootstrapConsole } from '@bielu/scalar-core'
import SignalRConsole from './components/SignalRConsole.vue'
import { SIGNALR_DISCOVERY } from './discovery'
import { SIGNALR_PLUGIN_SPEC } from './plugin'

export { createSignalRPlugin } from './plugin'
export { loadSignalRHubs, parseSignalRHubs } from './signalr-bindings'
export type {
  SignalRDirection,
  SignalRDocumentRef,
  SignalRHubModel,
  SignalROperationModel,
  SignalRPluginConfig,
} from './types'

// Register the console Web Component and hook `window.Scalar.createApiReference` so every Scalar
// API Reference on the page picks up the SignalR plugin (see @bielu/scalar-core's bootstrap).
bootstrapConsole(
  {
    ...SIGNALR_PLUGIN_SPEC,
    discovery: SIGNALR_DISCOVERY,
    stylesId: 'bielu-signalr-styles',
    wrappedFlag: '__bieluSignalRWrapped',
  },
  SignalRConsole,
)
