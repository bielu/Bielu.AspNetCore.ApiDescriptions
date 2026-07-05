import type { SecuritySchemeModel } from './types'

/**
 * Minimal structural copy of the PluginAuthState API exposed by the custom Scalar build
 * (feat/plugin-auth-state). Declaring it locally keeps the bundle free from a hard dependency on a
 * specific @scalar/types version; Scalar validates the plugin shape at runtime.
 *
 * On stock Scalar (where this API does not exist), `setAuthState` simply has nothing to store and
 * all callers of `resolveSignalRAuth` receive the no-op empty result.
 */
type AuthSecretKey =
  | 'x-scalar-secret-token'
  | 'x-scalar-secret-username'
  | 'x-scalar-secret-password'
  | 'x-scalar-secret-refresh-token'

type AuthSecrets = { type: string } & Partial<Record<AuthSecretKey, string>>

type PluginAuthState = {
  export: () => Record<string, unknown>
  getAuthSecrets: (documentName: string, schemeName: string) => AuthSecrets | undefined
  getAuthSelectedSchemas: (payload: { type: 'document'; documentName: string }) => string[]
}

let _authState: PluginAuthState | undefined

export function setAuthState(auth: unknown): void {
  if (isPluginAuthState(auth)) {
    _authState = auth
  }
}

export function getAuthState(): PluginAuthState | undefined {
  return _authState
}

function isPluginAuthState(value: unknown): value is PluginAuthState {
  return (
    value != null &&
    typeof value === 'object' &&
    typeof (value as PluginAuthState).getAuthSecrets === 'function' &&
    typeof (value as PluginAuthState).getAuthSelectedSchemas === 'function' &&
    typeof (value as PluginAuthState).export === 'function'
  )
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
 * options. Returns an empty result when auth state is absent (stock Scalar or no scheme selected),
 * so callers need no guard.
 *
 * Document-name matching tries an exact match first, then falls back to the sole key in the
 * exported auth state (covers cases where Scalar's internal key differs in case or normalisation
 * from the AsyncAPI document name).
 */
export function resolveSignalRAuth(
  documentName: string,
  securitySchemes: Record<string, SecuritySchemeModel> | undefined,
  auth: PluginAuthState | undefined,
): ResolvedAuth {
  const empty: ResolvedAuth = { queryParams: {}, warnings: [] }
  if (!auth || !securitySchemes) return empty

  let resolvedDocName = documentName
  // getAuthSelectedSchemas may return undefined on Scalar's empty auth state — guard with ?? []
  let schemas = auth.getAuthSelectedSchemas({ type: 'document', documentName }) ?? []
  if (schemas.length === 0) {
    // Exact match failed — fall back to an exported document key only when it is unambiguous:
    // the key matches the requested document after normalisation (case/slug differences), or
    // exactly one exported document has schemes selected. With several candidates we cannot tell
    // which credentials belong to this document, so skip rather than reuse another document's.
    const normalize = (value: string) => value.toLowerCase().replace(/[^a-z0-9]/g, '')
    const candidates = Object.keys(auth.export() ?? {})
      .map((key) => ({
        key,
        schemas: auth.getAuthSelectedSchemas({ type: 'document', documentName: key }) ?? [],
      }))
      .filter((candidate) => candidate.schemas.length > 0)
    const match =
      candidates.find((candidate) => normalize(candidate.key) === normalize(documentName)) ??
      (candidates.length === 1 ? candidates[0] : undefined)
    if (match) {
      resolvedDocName = match.key
      schemas = match.schemas
    }
  }
  if (schemas.length === 0) return empty

  const result: ResolvedAuth = { queryParams: {}, warnings: [] }

  for (const schemeName of schemas) {
    const secrets = auth.getAuthSecrets(resolvedDocName, schemeName)
    if (!secrets) continue

    // Only schemes declared in this hub's securitySchemes may contribute credentials — ignore
    // selections carried over from other documents or stale auth state.
    const schemeDef = securitySchemes[schemeName]
    if (!schemeDef) continue

    const type = secrets.type || schemeDef.type || ''
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
