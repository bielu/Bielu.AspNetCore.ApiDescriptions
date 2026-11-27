/**
 * Compile-time check that the plugin's locally declared shapes still match the plugin API Scalar
 * publishes. Nothing here runs: `npm run typecheck` fails if `@scalar/types` changes the contract.
 */
import type { ApiReferencePlugin as ScalarApiReferencePlugin, PluginDocumentType } from '@scalar/types/api-reference'

import { createLikeC4Plugin, likeC4DocumentType } from '../src/plugin'

export const documentType: PluginDocumentType = likeC4DocumentType
export const plugin: ScalarApiReferencePlugin = createLikeC4Plugin()
