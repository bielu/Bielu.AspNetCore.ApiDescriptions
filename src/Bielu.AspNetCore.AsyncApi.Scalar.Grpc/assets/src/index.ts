import { bootstrapConsole } from '@bielu/scalar-core'
import GrpcConsole from './components/GrpcConsole.vue'
import { GRPC_DISCOVERY } from './discovery'
import { captureBundleScriptSrc } from './grpc-client'
import { GRPC_PLUGIN_SPEC } from './plugin'

export { createGrpcPlugin } from './plugin'
export { loadGrpcServices, parseGrpcServices } from './grpc-bindings'
export { descriptorsUrlFor, loadDescriptorRegistry } from './grpc-client'
export type {
  GrpcDocumentRef,
  GrpcMethodModel,
  GrpcMethodTypeName,
  GrpcPluginConfig,
  GrpcServiceModel,
} from './types'

// Remember where this bundle was loaded from — the protobuf descriptor endpoint is served as a
// sibling of plugin.js. Must run now, while `document.currentScript` is still this script tag.
captureBundleScriptSrc()

// Register the console Web Component and hook `window.Scalar.createApiReference` so every Scalar
// API Reference on the page picks up the gRPC plugin (see @bielu/scalar-core's bootstrap).
bootstrapConsole(
  {
    ...GRPC_PLUGIN_SPEC,
    discovery: GRPC_DISCOVERY,
    stylesId: 'bielu-grpc-styles',
    wrappedFlag: '__bieluGrpcWrapped',
  },
  GrpcConsole,
)
