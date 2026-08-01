import type { DescMethod, DescService, FileRegistry, JsonValue } from '@bufbuild/protobuf'
import { createFileRegistry, fromBinary, fromJson, toJson } from '@bufbuild/protobuf'
import { FileDescriptorSetSchema } from '@bufbuild/protobuf/wkt'
import { createClient } from '@connectrpc/connect'
import { createGrpcWebTransport } from '@connectrpc/connect-web'

/** The default base path `MapScalarGrpcAssets()` serves the bundle and descriptors from. */
export const DEFAULT_ASSETS_PATH = '/bielu/scalar/grpc'

// The URL this bundle was loaded from, captured at script-evaluation time (see `index.ts`). The
// descriptor endpoint is served as a sibling of plugin.js, so this is the primary way to find it.
let bundleScriptSrc: string | undefined

/**
 * Capture the bundle's own script URL. Must be called at module-evaluation time, while
 * `document.currentScript` still points at the executing `<script>` tag.
 */
export function captureBundleScriptSrc(): void {
  if (typeof document !== 'undefined') {
    const src = (document.currentScript as HTMLScriptElement | null)?.src
    if (src) {
      bundleScriptSrc = src
    }
  }
}

/**
 * Set the bundle's own URL explicitly, for the ESM entry point loaded via Scalar's `pluginUrls`.
 *
 * `document.currentScript` is always `null` inside an ES module, so the module passes its
 * `import.meta.url` here instead — otherwise `descriptorsUrlFor` would lose its same-origin
 * shortcut and always fall back to the default assets path.
 */
export function setBundleScriptSrc(url: string | undefined): void {
  if (url) {
    bundleScriptSrc = url
  }
}

/**
 * The URL of the protobuf descriptor endpoint for a target base URL.
 *
 * When the bundle was served by `MapScalarGrpcAssets()` from the same origin as the target, the
 * descriptors live next to plugin.js (whatever custom path it was mapped under). When the bundle
 * came from elsewhere (e.g. a CDN in the Aspire setup), fall back to the default assets path on
 * the target origin.
 */
export function descriptorsUrlFor(baseUrl: string): string {
  const normalizedBase = baseUrl.replace(/\/+$/, '')
  if (bundleScriptSrc && typeof window !== 'undefined') {
    try {
      const script = new URL(bundleScriptSrc, window.location.href)
      const target = new URL(normalizedBase || window.location.origin, window.location.href)
      if (script.origin === target.origin) {
        return new URL('descriptors', script.href).href
      }
    } catch {
      // Fall through to the default path on the target origin.
    }
  }
  return `${normalizedBase}${DEFAULT_ASSETS_PATH}/descriptors`
}

// Cache the parsed registry per descriptor URL; failed loads are evicted so a later invoke retries.
const registryCache = new Map<string, Promise<FileRegistry>>()

async function fetchRegistry(url: string): Promise<FileRegistry> {
  const response = await fetch(url, { headers: { accept: 'application/x-protobuf' } })
  if (!response.ok) {
    throw new Error(
      `Failed to load protobuf descriptors from ${url} (HTTP ${response.status}). ` +
        `Ensure the target app calls MapScalarGrpcAssets().`,
    )
  }
  const bytes = new Uint8Array(await response.arrayBuffer())
  return createFileRegistry(fromBinary(FileDescriptorSetSchema, bytes))
}

/** Load (and cache) the protobuf descriptor registry served by the .NET package. */
export function loadDescriptorRegistry(url: string): Promise<FileRegistry> {
  let cached = registryCache.get(url)
  if (!cached) {
    cached = fetchRegistry(url)
    cached.catch(() => registryCache.delete(url))
    registryCache.set(url, cached)
  }
  return cached
}

/** Resolve a service and RPC method from the descriptor registry, with actionable errors. */
export function resolveMethod(
  registry: FileRegistry,
  serviceName: string,
  methodName: string,
): { service: DescService; method: DescMethod } {
  const service = registry.getService(serviceName)
  if (!service) {
    throw new Error(
      `Service "${serviceName}" was not found in the server's descriptors — ` +
        `is it mapped via MapGrpcService<T>()?`,
    )
  }
  const method = service.methods.find((candidate) => candidate.name === methodName)
  if (!method) {
    throw new Error(`Method "${methodName}" was not found on service "${serviceName}".`)
  }
  return { service, method }
}

export type GrpcCallOptions = {
  /** The target origin (scheme://host) the gRPC-Web calls are sent to. */
  baseUrl: string
  /** Call metadata — gRPC-Web metadata is plain HTTP headers. */
  headers?: Record<string, string>
  /** Call deadline in milliseconds. */
  timeoutMs?: number
}

type DynamicClient = Record<string, (input: unknown, options?: unknown) => unknown>

function clientFor(service: DescService, options: GrpcCallOptions): DynamicClient {
  const transport = createGrpcWebTransport({ baseUrl: options.baseUrl })
  // The registry-derived DescService is untyped, so the generated client surface degrades to a
  // dynamic method map — exactly what a console driven by runtime descriptors needs.
  return createClient(service, transport) as unknown as DynamicClient
}

/** Invoke a unary RPC with a proto3-JSON request; returns the response as proto3 JSON. */
export async function invokeUnary(
  registry: FileRegistry,
  serviceName: string,
  methodName: string,
  requestJson: JsonValue,
  options: GrpcCallOptions,
): Promise<JsonValue> {
  const { service, method } = resolveMethod(registry, serviceName, methodName)
  const request = fromJson(method.input, requestJson, { registry })
  const client = clientFor(service, options)
  const response = await client[method.localName](request, {
    headers: options.headers,
    timeoutMs: options.timeoutMs,
  })
  return toJson(method.output, response as never, { registry })
}

/**
 * Invoke a server-streaming RPC with a proto3-JSON request. Each response message is passed to
 * `onMessage` as proto3 JSON; resolves with the total message count once the stream completes.
 */
export async function invokeServerStreaming(
  registry: FileRegistry,
  serviceName: string,
  methodName: string,
  requestJson: JsonValue,
  options: GrpcCallOptions,
  onMessage: (message: JsonValue) => void,
): Promise<number> {
  const { service, method } = resolveMethod(registry, serviceName, methodName)
  const request = fromJson(method.input, requestJson, { registry })
  const client = clientFor(service, options)
  const stream = client[method.localName](request, {
    headers: options.headers,
    timeoutMs: options.timeoutMs,
  }) as AsyncIterable<unknown>

  let count = 0
  for await (const message of stream) {
    count += 1
    onMessage(toJson(method.output, message as never, { registry }))
  }
  return count
}
