/**
 * Compile-time check that the plugin's locally declared shapes still match the plugin API Scalar
 * publishes. Nothing here runs: `npm run typecheck` fails if `@scalar/types` changes the contract.
 */
import type { ApiReferencePlugin as ScalarApiReferencePlugin, PluginDocumentType } from '@scalar/types/api-reference'

import { arazzoDocumentType, createArazzoPlugin } from '../src/plugin'

export const documentType: PluginDocumentType = arazzoDocumentType
export const plugin: ScalarApiReferencePlugin = createArazzoPlugin()
