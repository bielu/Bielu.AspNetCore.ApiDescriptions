import type { SecuritySchemeModel } from './types'

/**
 * Minimal structural copy of the PluginAuthState API exposed by the custom Scalar build
 * (feat/plugin-auth-state). Declaring it locally keeps the bundles free from a hard dependency on a
 * specific @scalar/types version; Scalar validates the plugin shape at runtime.
 *
 * On stock Scalar (where this API does not exist), `setAuthState` simply has nothing to store and
 * all callers of `resolveSelectedSchemes` receive the no-op empty result.
 */
type AuthSecretKey =
  | 'x-scalar-secret-token'
  | 'x-scalar-secret-username'
  | 'x-scalar-secret-password'
  | 'x-scalar-secret-refresh-token'

export type AuthSecrets = { type: string } & Partial<Record<AuthSecretKey, string>>

export type PluginAuthState = {
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

/** One security scheme the user selected in Scalar, with its secrets and declared definition. */
export type SelectedScheme = {
  schemeName: string
  /** The resolved scheme type — the secrets' type falling back to the declared scheme type. */
  type: string
  /** The scheme as declared in the AsyncAPI document's `securitySchemes`. */
  scheme: SecuritySchemeModel
  secrets: AuthSecrets
}

/**
 * Resolve Scalar's selected auth for the given document to the schemes (and secrets) that apply.
 * Protocol packages map the result onto their transport (query params for WebSocket, headers for
 * gRPC-Web, ...). Returns an empty result when auth state is absent (stock Scalar or no scheme
 * selected), so callers need no guard.
 *
 * Document-name matching tries an exact match first, then falls back to the sole key in the
 * exported auth state (covers cases where Scalar's internal key differs in case or normalisation
 * from the AsyncAPI document name).
 */
export function resolveSelectedSchemes(
  documentName: string,
  securitySchemes: Record<string, SecuritySchemeModel> | undefined,
  auth: unknown,
): SelectedScheme[] {
  if (!isPluginAuthState(auth) || !securitySchemes) return []

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
  if (schemas.length === 0) return []

  const selected: SelectedScheme[] = []
  for (const schemeName of schemas) {
    const secrets = auth.getAuthSecrets(resolvedDocName, schemeName)
    if (!secrets) continue

    // Only schemes declared in this document's securitySchemes may contribute credentials — ignore
    // selections carried over from other documents or stale auth state.
    const scheme = securitySchemes[schemeName]
    if (!scheme) continue

    selected.push({ schemeName, type: secrets.type || scheme.type || '', scheme, secrets })
  }
  return selected
}
