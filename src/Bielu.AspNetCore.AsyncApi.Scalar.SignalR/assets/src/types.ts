/** Direction of a SignalR hub operation relative to the server. */
export type SignalRDirection = 'clientToServer' | 'serverToClient'

/** A reference to an AsyncAPI document — either by URL or as an already-parsed object. */
export type SignalRDocumentRef = {
  /** Logical document name (matches the AsyncAPI/source title). */
  name: string
  /** URL the AsyncAPI JSON document is served from. */
  url?: string
  /** An inline, already-parsed document object (from a Scalar source's `content`). */
  doc?: Record<string, any>
}

/** Configuration consumed by the SignalR console plugin. */
export type SignalRPluginConfig = {
  /**
   * AsyncAPI documents to scan for SignalR bindings. Normally derived automatically from the
   * Scalar configuration's `sources`; only set explicitly to override that discovery.
   */
  documents: SignalRDocumentRef[]
}

/** A single hub operation (client-to-server method or server-to-client event). */
export type SignalROperationModel = {
  /** AsyncAPI operation id. */
  id: string
  /** The hub method / event name actually used on the wire. */
  target: string
  direction: SignalRDirection
  /** `invocation`, `streamInvocation` or `send`. */
  callType?: string
  summary?: string
}

/** A SignalR hub discovered from an AsyncAPI channel with a `signalr` binding. */
export type SignalRHubModel = {
  documentName: string
  channelName: string
  /** Hub path, e.g. `/chatHub`. */
  hubPath: string
  /** Best-effort absolute base URL (scheme://host) resolved from the AsyncAPI servers. */
  baseUrl: string
  transports: string[]
  protocols: string[]
  operations: SignalROperationModel[]
}
