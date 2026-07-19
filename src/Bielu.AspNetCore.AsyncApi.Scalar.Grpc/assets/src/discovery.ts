import { resolveDocuments as resolveDocumentsCore } from '@bielu/scalar-core'
import type { GrpcDocumentRef } from './types'

/** How the gRPC bundle discovers its AsyncAPI documents (see `resolveDocuments` in @bielu/scalar-core). */
export const GRPC_DISCOVERY = {
  configKey: 'grpc',
  globalName: '__BIELU_SCALAR_GRPC__',
} as const

/**
 * Resolves the documents to scan, in decreasing priority: an explicit override (`documents` prop);
 * a `GrpcPluginConfig` embedded inline on the Scalar config as `config.grpc`; a
 * `window.__BIELU_SCALAR_GRPC__` global; otherwise auto-discovery from the Scalar configuration
 * passed as `options`.
 */
export function resolveDocuments(
  options: Record<string, any> | undefined,
  override?: GrpcDocumentRef[],
): GrpcDocumentRef[] {
  return resolveDocumentsCore(options, GRPC_DISCOVERY, override)
}
