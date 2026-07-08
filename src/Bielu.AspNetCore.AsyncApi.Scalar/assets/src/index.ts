export { getAuthState, resolveSelectedSchemes, setAuthState } from './auth'
export type { AuthSecrets, PluginAuthState, SelectedScheme } from './auth'
export { bootstrapConsole, installScalarHook, registerConsoleElement } from './bootstrap'
export { documentsFromScalarConfig, resolveDocuments } from './discovery'
export {
  deref,
  extractSecuritySchemes,
  firstServerHost,
  loadAsyncApiDocuments,
  pointer,
  refName,
  resolveServerBaseUrl,
} from './documents'
export type { LoadedDocument } from './documents'
export { createConsolePlugin } from './plugin'
export type { ApiReferencePlugin } from './plugin'
export { exampleFromSchema } from './schema-example'
export type {
  ConsoleBundleSpec,
  ConsolePluginSpec,
  DiscoveryOptions,
  DocumentRef,
  SecuritySchemeModel,
} from './types'
