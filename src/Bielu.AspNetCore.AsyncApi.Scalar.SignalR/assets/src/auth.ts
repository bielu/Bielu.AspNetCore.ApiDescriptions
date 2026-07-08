import {
  getAuthState as coreGetAuthState,
  resolveSelectedSchemes,
  setAuthState as coreSetAuthState,
} from '@bielu/scalar-core'
import type { SecuritySchemeModel } from './types'

// Thin wrappers around @bielu/scalar-core's auth-state store. Typed as `unknown` so the published
// declaration files never reference the private core package (see the core README).

/** Store Scalar's PluginAuthState (no-op on stock Scalar, where the API does not exist). */
export function setAuthState(auth: unknown): void {
  coreSetAuthState(auth)
}

/** The captured PluginAuthState, as an opaque handle for `resolveSignalRAuth`. */
export function getAuthState(): unknown {
  return coreGetAuthState()
}

export type ResolvedAuth = {
  /** Bearer token to supply via `accessTokenFactory` (http bearer, oauth2, openIdConnect). */
  accessToken?: string
  /** Key-value pairs to append as query parameters to the hub URL. */
  queryParams: Record<string, string>
  /** Non-fatal warnings about scheme constraints (e.g. header-only schemes that WS cannot send). */
  warnings: string[]
}

/**
 * Resolve Scalar's selected auth for the given document and map the secrets to SignalR connection
 * options. Browser WebSocket/SSE cannot send arbitrary headers, so API keys are mapped onto query
 * parameters (with a warning when the scheme declared a header) and only bearer-shaped schemes can
 * supply an access token. Returns an empty result when auth state is absent (stock Scalar or no
 * scheme selected), so callers need no guard.
 */
export function resolveSignalRAuth(
  documentName: string,
  securitySchemes: Record<string, SecuritySchemeModel> | undefined,
  auth: unknown,
): ResolvedAuth {
  const result: ResolvedAuth = { queryParams: {}, warnings: [] }

  for (const { schemeName, type, scheme: schemeDef, secrets } of resolveSelectedSchemes(
    documentName,
    securitySchemes,
    auth,
  )) {
    const token = secrets['x-scalar-secret-token']

    switch (type) {
      case 'apiKey':
      case 'httpApiKey': {
        const location = (schemeDef.in ?? 'query').toLowerCase()
        const paramName = schemeDef.name ?? 'api_key'
        if (location === 'cookie') {
          result.warnings.push(
            `API key scheme "${schemeName}" targets cookie "${paramName}", which the browser ` +
              `attaches automatically — not appending it to the URL. Ensure the cookie is set ` +
              `for this origin.`,
          )
          break
        }
        if (location === 'header') {
          result.warnings.push(
            `API key scheme "${schemeName}" targets header "${paramName}", but browser WebSocket/SSE ` +
              `cannot set arbitrary headers — appending as a query param instead. ` +
              `Ensure the server also accepts "${paramName}" as a query parameter.`,
          )
        }
        if (token) result.queryParams[paramName] = token
        break
      }
      case 'http': {
        const scheme = schemeDef?.scheme?.toLowerCase() ?? ''
        if (scheme === 'bearer' || scheme === '') {
          if (token) result.accessToken = token
        } else if (scheme === 'basic') {
          result.warnings.push(
            `HTTP Basic scheme "${schemeName}" cannot be sent over browser WebSocket/SSE — ` +
              `connect will proceed without credentials.`,
          )
        }
        break
      }
      case 'oauth2':
      case 'openIdConnect':
        if (token) result.accessToken = token
        break
    }
  }

  return result
}
