import { bootstrapConsole } from '@bielu/scalar-core'
import { captureBundleScriptSrc } from './broker-client'
import BrokerConsole from './components/BrokerConsole.vue'
import { BROKER_DISCOVERY } from './discovery'
import { BROKER_PLUGIN_SPEC } from './plugin'

export { createBrokerPlugin } from './plugin'
export { loadBrokerDocuments, parseBrokerChannels } from './broker-bindings'
export type { BrokerDocumentModel } from './broker-bindings'
export { loadConnections, proxyBaseUrlFor, publish, tail } from './broker-client'
export type {
  BrokerChannelModel,
  BrokerConnection,
  BrokerDocumentRef,
  BrokerPluginConfig,
  BrokerProtocol,
  BrokerPublishReceipt,
  BrokerTailMessage,
} from './types'

// Remember where this bundle was loaded from — the proxy endpoints are served as siblings of
// plugin.js. Must run now, while `document.currentScript` is still this script tag.
captureBundleScriptSrc()

// Register the console Web Component and hook `window.Scalar.createApiReference` so every Scalar
// API Reference on the page picks up the broker plugin (see @bielu/scalar-core's bootstrap).
bootstrapConsole(
  {
    ...BROKER_PLUGIN_SPEC,
    discovery: BROKER_DISCOVERY,
    stylesId: 'bielu-broker-styles',
    wrappedFlag: '__bieluBrokerWrapped',
  },
  BrokerConsole,
)
