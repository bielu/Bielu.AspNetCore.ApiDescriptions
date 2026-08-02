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

/** The captured PluginAuthState, as an opaque handle for `resolveBrokerAuth`. */
export function getAuthState(): unknown {
  return coreGetAuthState()
}

export type ResolvedAuth = {
  /** Headers to send with each proxy call. */
  headers: Record<string, string>
  /** Query-string parameters to append to each proxy call. */
  query: Record<string, string>
  /** Non-fatal warnings about scheme constraints. */
  warnings: string[]
}

/**
 * Resolve Scalar's selected auth for the given document into credentials for the bridge's proxy
 * endpoints.
 *
 * Unlike the gRPC console, these are ordinary HTTP requests, so a query-based API key can be sent
 * as an actual query parameter rather than being downgraded to a header. Returns an empty result
 * when auth state is absent (stock Scalar or no scheme selected), so callers need no guard.
 */
export function resolveBrokerAuth(
  documentName: string,
  securitySchemes: Record<string, SecuritySchemeModel> | undefined,
  auth: unknown,
): ResolvedAuth {
  const result: ResolvedAuth = { headers: {}, query: {}, warnings: [] }

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
        if (token) {
          if (location === 'query') {
            result.query[paramName] = token
          } else {
            result.headers[paramName] = token
          }
        }
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

/** Appends the resolved query credentials to a URL. */
export function withAuthQuery(url: string, auth: ResolvedAuth): string {
  const entries = Object.entries(auth.query)
  if (entries.length === 0) {
    return url
  }
  const separator = url.includes('?') ? '&' : '?'
  const query = entries
    .map(([key, value]) => `${encodeURIComponent(key)}=${encodeURIComponent(value)}`)
    .join('&')
  return `${url}${separator}${query}`
}
