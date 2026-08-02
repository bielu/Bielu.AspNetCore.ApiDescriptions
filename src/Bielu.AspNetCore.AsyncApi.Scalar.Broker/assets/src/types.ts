/** A reference to an AsyncAPI document the console scans for broker bindings. */
export type BrokerDocumentRef = {
  /** Logical document name. */
  name: string
  /** URL the AsyncAPI JSON document is served from. */
  url?: string
  /** An inline, already-parsed document object (from a Scalar source's `content`). */
  doc?: Record<string, any>
}

/** A minimal representation of an AsyncAPI security scheme, extracted at document-parse time. */
export type SecuritySchemeModel = {
  /** e.g. `'apiKey'`, `'http'`, `'oauth2'`, `'openIdConnect'`. */
  type: string
  /** For `apiKey`: `'query'`, `'header'`, or `'cookie'`. */
  in?: string
  /** For `apiKey`: the query-param or header field name. */
  name?: string
  /** For `http`: `'bearer'`, `'basic'`, etc. */
  scheme?: string
}

/** Inline plugin configuration, read from `config.broker` or the window global. */
export type BrokerPluginConfig = {
  documents?: BrokerDocumentRef[]
}

/** The protocols the bridge can drive. Matches the AsyncAPI protocol identifiers. */
export type BrokerProtocol = 'kafka' | 'mqtt' | 'amqp'

/** A connection the server-side bridge exposes, as returned by `GET {assets}/connections`. */
export type BrokerConnection = {
  name: string
  protocol: string
  /** Display-only; the bridge never sends credentials to the browser. */
  endpoint: string
}

/** One publishable/tailable channel discovered from a document's broker bindings. */
export type BrokerChannelModel = {
  /** The AsyncAPI channel id. */
  id: string
  /** The wire-level channel name (Kafka topic, MQTT topic, AMQP queue) the bridge addresses. */
  address: string
  protocol: BrokerProtocol
  description?: string
  /** The document this channel came from, for grouping in the UI. */
  documentName: string
  /** A prefilled request body derived from the channel's message payload schema, when there is one. */
  example?: string
}

/** The broker's acknowledgement of a published message. */
export type BrokerPublishReceipt = {
  channel: string
  timestamp: string
  partition?: number | null
  offset?: number | null
}

/** A message received on the tail stream. */
export type BrokerTailMessage = {
  channel: string
  key?: string | null
  headers: Record<string, string>
  payload: string
  timestamp: string
  partition?: number | null
  offset?: number | null
}
