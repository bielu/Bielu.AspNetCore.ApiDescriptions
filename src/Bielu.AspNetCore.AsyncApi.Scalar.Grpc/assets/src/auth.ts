import {
  getAuthState as coreGetAuthState,
  resolveSelectedSchemes,
  setAuthState as coreSetAuthState,
} from '@bielu/scalar-core'
import type { SecuritySchemeModel } from './types'

// Thin wrappers around @bielu/scalar-core's auth-state store. Typed as `unknown` so the published
// declaration files never reference the private core package (see the core README).

/** Store Scalar's PluginAuthState (no-op on older Scalar versions that predate the API). */
export function setAuthState(auth: unknown): void {
  coreSetAuthState(auth)
}

/** The captured PluginAuthState, as an opaque handle for `resolveGrpcAuth`. */
export function getAuthState(): unknown {
  return coreGetAuthState()
}

export type ResolvedAuth = {
  /** Metadata (HTTP headers) to send with each gRPC-Web call. */
  headers: Record<string, string>
  /** Non-fatal warnings about scheme constraints (e.g. query-based keys gRPC-Web cannot send). */
  warnings: string[]
}

/**
 * Resolve Scalar's selected auth for the given document and map the secrets to gRPC-Web call
 * metadata. gRPC-Web metadata is plain HTTP headers, so header API keys, bearer tokens and HTTP
 * Basic all map directly; query-based API keys have no place on a gRPC-Web request and only
 * produce a warning. Returns an empty result when auth state is absent (stock Scalar or no scheme
 * selected), so callers need no guard.
 */
export function resolveGrpcAuth(
  documentName: string,
  securitySchemes: Record<string, SecuritySchemeModel> | undefined,
  auth: unknown,
): ResolvedAuth {
  const result: ResolvedAuth = { headers: {}, warnings: [] }

  for (const { schemeName, type, scheme: schemeDef, secrets } of resolveSelectedSchemes(
    documentName,
    securitySchemes,
    auth,
  )) {
    const token = secrets['x-scalar-secret-token']

    switch (type) {
      case 'apiKey':
      case 'httpApiKey': {
        const location = (schemeDef.in ?? 'header').toLowerCase()
        const paramName = schemeDef.name ?? 'api_key'
        if (location === 'cookie') {
          result.warnings.push(
            `API key scheme "${schemeName}" targets cookie "${paramName}", which the browser ` +
              `attaches automatically — not adding a header. Ensure the cookie is set for the ` +
              `target origin.`,
          )
          break
        }
        if (location === 'query') {
          result.warnings.push(
            `API key scheme "${schemeName}" targets query parameter "${paramName}", but gRPC-Web ` +
              `requests carry no query string — sending it as a "${paramName}" header instead. ` +
              `Ensure the server also accepts "${paramName}" as a header.`,
          )
        }
        if (token) result.headers[paramName] = token
        break
      }
      case 'http': {
        const scheme = schemeDef?.scheme?.toLowerCase() ?? ''
        if (scheme === 'bearer' || scheme === '') {
          if (token) result.headers['Authorization'] = `Bearer ${token}`
        } else if (scheme === 'basic') {
          const username = secrets['x-scalar-secret-username'] ?? ''
          const password = secrets['x-scalar-secret-password'] ?? ''
          if (username || password) {
            result.headers['Authorization'] = `Basic ${btoa(`${username}:${password}`)}`
          }
        }
        break
      }
      case 'oauth2':
      case 'openIdConnect':
        if (token) result.headers['Authorization'] = `Bearer ${token}`
        break
    }
  }

  return result
}
