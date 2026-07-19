/** The four kinds of RPC a gRPC method can be (camelCase wire tokens from the `grpc` binding). */
export type GrpcMethodTypeName =
  | 'unary'
  | 'serverStreaming'
  | 'clientStreaming'
  | 'bidirectionalStreaming'

/** A reference to an AsyncAPI document — either by URL or as an already-parsed object. */
export type GrpcDocumentRef = {
  /** Logical document name (matches the AsyncAPI/source title). */
  name: string
  /** URL the AsyncAPI JSON document is served from. */
  url?: string
  /** An inline, already-parsed document object (from a Scalar source's `content`). */
  doc?: Record<string, any>
}

/** Configuration consumed by the gRPC console plugin. */
export type GrpcPluginConfig = {
  /**
   * AsyncAPI documents to scan for gRPC bindings. Normally derived automatically from the
   * Scalar configuration's `sources`; only set explicitly to override that discovery.
   */
  documents: GrpcDocumentRef[]
}

/** A message an RPC method can carry, with a generated example payload. */
export type GrpcMessageModel = {
  /** Message name/key (e.g. `helloRequest`). */
  name: string
  /** Human-friendly title, when the document provides one. */
  title?: string
  /**
   * A ready-to-send example of the request message, as a pretty-printed JSON object string
   * (proto3 JSON mapping), generated from the message payload schema.
   */
  example: string
}

/** A single RPC method discovered from an AsyncAPI operation with a `grpc` binding. */
export type GrpcMethodModel = {
  /** AsyncAPI operation id. */
  id: string
  /** The RPC method name actually used on the wire, e.g. `SayHello`. */
  method: string
  /** The kind of RPC. Missing method types are treated as unary. */
  methodType?: GrpcMethodTypeName
  /** The fully-qualified protobuf type of the request message, e.g. `greet.HelloRequest`. */
  requestType?: string
  /** The fully-qualified protobuf type of the response message, e.g. `greet.HelloReply`. */
  responseType?: string
  /** The declared idempotency level (e.g. `noSideEffects`). */
  idempotencyLevel?: string
  /** The call deadline in seconds, when the operation declares one. */
  deadlineSeconds?: number
  summary?: string
  /** Messages this operation carries, each with an example payload to prefill the editor. */
  messages: GrpcMessageModel[]
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

/** A gRPC service discovered from an AsyncAPI channel with a `grpc` binding. */
export type GrpcServiceModel = {
  documentName: string
  channelName: string
  /** The fully-qualified service name, e.g. `greet.Greeter`. */
  service: string
  /** The protobuf package the service belongs to, e.g. `greet`. */
  package?: string
  /** Path or URL to the `.proto` file that defines the service. */
  protoFile?: string
  /** Best-effort absolute base URL (scheme://host) resolved from the AsyncAPI servers. */
  baseUrl: string
  methods: GrpcMethodModel[]
  /** Security schemes declared in this service's AsyncAPI document, keyed by scheme name. */
  securitySchemes?: Record<string, SecuritySchemeModel>
}
