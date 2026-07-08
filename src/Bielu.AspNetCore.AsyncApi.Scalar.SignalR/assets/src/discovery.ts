import { resolveDocuments as resolveDocumentsCore } from '@bielu/scalar-core'
import type { SignalRDocumentRef } from './types'

/** How the SignalR bundle discovers its AsyncAPI documents (see `resolveDocuments` in @bielu/scalar-core). */
export const SIGNALR_DISCOVERY = {
  configKey: 'signalr',
  globalName: '__BIELU_SCALAR_SIGNALR__',
} as const

/**
 * Resolves the documents to scan, in decreasing priority: an explicit override (`documents` prop);
 * a `SignalRPluginConfig` embedded inline on the Scalar config as `config.signalr`; a
 * `window.__BIELU_SCALAR_SIGNALR__` global; otherwise auto-discovery from the Scalar configuration
 * passed as `options`.
 */
export function resolveDocuments(
  options: Record<string, any> | undefined,
  override?: SignalRDocumentRef[],
): SignalRDocumentRef[] {
  return resolveDocumentsCore(options, SIGNALR_DISCOVERY, override)
}
