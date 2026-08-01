export { getAuthState, resolveSelectedSchemes, setAuthState } from './auth'
export type { AuthSecrets, PluginAuthState, SelectedScheme } from './auth'
export { bootstrapConsole, installScalarHook, registerConsoleElement } from './bootstrap'
export { documentsFromScalarConfig, isDocumentRefArray, resolveDocuments } from './discovery'
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
export { createPluginModule, documentsFromModuleUrl } from './plugin-module'
export { exampleFromSchema } from './schema-example'
export type {
  ConsoleBundleSpec,
  ConsoleModuleSpec,
  ConsolePluginSpec,
  DiscoveryOptions,
  DocumentRef,
  SecuritySchemeModel,
} from './types'
