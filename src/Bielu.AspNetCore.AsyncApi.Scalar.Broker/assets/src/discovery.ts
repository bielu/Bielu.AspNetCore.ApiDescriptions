import { resolveDocuments as resolveDocumentsCore } from '@bielu/scalar-core'
import type { BrokerDocumentRef } from './types'

/** How the broker bundle discovers its AsyncAPI documents (see `resolveDocuments` in @bielu/scalar-core). */
export const BROKER_DISCOVERY = {
  configKey: 'broker',
  globalName: '__BIELU_SCALAR_BROKER__',
} as const

/**
 * Resolves the documents to scan, in decreasing priority: an explicit override (`documents` prop);
 * a `BrokerPluginConfig` embedded inline on the Scalar config as `config.broker`; a
 * `window.__BIELU_SCALAR_BROKER__` global; otherwise auto-discovery from the Scalar configuration
 * passed as `options`.
 */
export function resolveDocuments(
  options: Record<string, any> | undefined,
  override?: BrokerDocumentRef[],
): BrokerDocumentRef[] {
  return resolveDocumentsCore(options, BROKER_DISCOVERY, override)
}
