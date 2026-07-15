import type { SecuritySchemeModel } from './types'

/**
 * Minimal structural copy of the PluginAuthState API Scalar exposes to plugins (upstreamed in
 * scalar/scalar#9639, shipped in Scalar.AspNetCore ≥ 2.16.12). Declaring it locally keeps the bundles
 * free from a hard dependency on a specific @scalar/types version; Scalar validates the plugin shape
 * at runtime.
 *
 * On older Scalar versions that predate the API, `setAuthState` simply has nothing to store and all
 * callers of `resolveSelectedSchemes` receive the no-op empty result.
 */
type AuthSecretKey =
  | 'x-scalar-secret-token'
  | 'x-scalar-secret-username'
  | 'x-scalar-secret-password'
  | 'x-scalar-secret-refresh-token'

export type AuthSecrets = { type: string } & Partial<Record<AuthSecretKey, string>>

/**
 * Scalar's selected security for a document (or operation). Each entry in `selectedSchemes` is a
 * security *requirement* mapping a scheme name to its selected scopes; the scheme names are the
 * object keys. Mirrors `SelectedSecurity` in `@scalar/workspace-store`.
 */
export type PluginSelectedSecurity = {
  selectedIndex: number
  selectedSchemes: Array<Record<string, string[]>>
}

export type PluginAuthState = {
  export: () => Record<string, unknown>
  getAuthSecrets: (documentName: string, schemeName: string) => AuthSecrets | undefined
  getAuthSelectedSchemas: (payload: {
    type: 'document'
    documentName: string
  }) => PluginSelectedSecurity | undefined
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

/** The scheme names in a Scalar `SelectedSecurity`, flattened across its requirement objects. */
function selectedSchemeNames(selected: PluginSelectedSecurity | undefined): string[] {
  if (!selected || !Array.isArray(selected.selectedSchemes)) return []
  return selected.selectedSchemes.flatMap((requirement) =>
    requirement && typeof requirement === 'object' ? Object.keys(requirement) : [],
  )
}

/** True when a secrets record carries at least one non-empty secret value (`x-scalar-secret-*`). */
function hasSecretValue(secrets: AuthSecrets): boolean {
  return Object.entries(secrets).some(
    ([key, value]) => key.startsWith('x-scalar-secret-') && typeof value === 'string' && value.length > 0,
  )
}

/**
 * Resolve the auth-store key for a document: an exact match first, then a normalised match
 * (case/slug differences). Returns the requested name unchanged when nothing matches, so the secret
 * lookups below simply find nothing — never falling back to another document's stored credentials.
 */
function resolveDocumentKey(auth: PluginAuthState, documentName: string): string {
  const store = auth.export() ?? {}
  if (store[documentName]) return documentName
  const normalize = (value: string) => value.toLowerCase().replace(/[^a-z0-9]/g, '')
  const keys = Object.keys(store)
  const normalized = keys.find((key) => normalize(key) === normalize(documentName))
  if (normalized) return normalized
  return documentName
}

/**
 * Resolve Scalar's selected auth for the given document to the schemes (and secrets) that apply.
 * Protocol packages map the result onto their transport (query params for WebSocket, headers for
 * gRPC-Web, ...). Returns an empty result when auth state is absent (stock Scalar or no credentials
 * entered), so callers need no guard.
 *
 * Selection resolution: Scalar only persists a document-level selection once the user *changes* the
 * auth picker; the pre-selected (required) scheme's secrets are stored without a selection being
 * written back. So when there is no explicit selection we fall back to every scheme the document
 * declares and keep the ones that actually have a stored secret — which is what the user typed into
 * the Authentication panel.
 */
export function resolveSelectedSchemes(
  documentName: string,
  securitySchemes: Record<string, SecuritySchemeModel> | undefined,
  auth: unknown,
): SelectedScheme[] {
  if (!isPluginAuthState(auth) || !securitySchemes) return []

  const resolvedDocName = resolveDocumentKey(auth, documentName)

  const selectedNames = selectedSchemeNames(
    auth.getAuthSelectedSchemas({ type: 'document', documentName: resolvedDocName }),
  )
  // Fall back to every declared scheme when Scalar has no explicit selection stored.
  const candidateNames = selectedNames.length > 0 ? selectedNames : Object.keys(securitySchemes)

  const selected: SelectedScheme[] = []
  for (const schemeName of candidateNames) {
    // Only schemes declared in this document's securitySchemes may contribute credentials — ignore
    // selections carried over from other documents or stale auth state.
    const scheme = securitySchemes[schemeName]
    if (!scheme) continue

    // Skip schemes with no secret actually entered, so an unconfigured scheme in the fallback path
    // does not masquerade as resolved credentials.
    const secrets = auth.getAuthSecrets(resolvedDocName, schemeName)
    if (!secrets || !hasSecretValue(secrets)) continue

    selected.push({ schemeName, type: secrets.type || scheme.type || '', scheme, secrets })
  }
  return selected
}
