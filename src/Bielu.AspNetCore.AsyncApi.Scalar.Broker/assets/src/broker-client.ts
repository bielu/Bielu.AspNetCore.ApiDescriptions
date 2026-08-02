import type { ResolvedAuth } from './auth'
import { withAuthQuery } from './auth'
import type { BrokerConnection, BrokerPublishReceipt, BrokerTailMessage } from './types'

/** The default base path `MapScalarBrokerAssets()` serves the bundle and proxy endpoints from. */
export const DEFAULT_ASSETS_PATH = '/bielu/scalar/broker'

// The URL this bundle was loaded from, captured at script-evaluation time (see `index.ts`). The
// proxy endpoints are served as siblings of plugin.js, so this is the primary way to find them.
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
 * The base URL of the proxy endpoints for a target base URL.
 *
 * When the bundle was served by `MapScalarBrokerAssets()` from the same origin as the target, the
 * endpoints live next to plugin.js (whatever custom path it was mapped under). When the bundle came
 * from elsewhere, fall back to the default assets path on the target origin.
 */
export function proxyBaseUrlFor(baseUrl: string): string {
  const normalizedBase = baseUrl.replace(/\/+$/, '')
  if (bundleScriptSrc && typeof window !== 'undefined') {
    try {
      const script = new URL(bundleScriptSrc, window.location.href)
      const target = new URL(normalizedBase || window.location.origin, window.location.href)
      if (script.origin === target.origin) {
        // Strip the plugin.js filename, keeping whatever base path it was mapped under.
        return new URL('.', script.href).href.replace(/\/+$/, '')
      }
    } catch {
      // Fall through to the default path on the target origin.
    }
  }
  return `${normalizedBase}${DEFAULT_ASSETS_PATH}`
}

async function problemText(response: Response): Promise<string> {
  try {
    const body = (await response.json()) as { detail?: string; title?: string }
    return body.detail ?? body.title ?? `HTTP ${response.status}`
  } catch {
    return `HTTP ${response.status}`
  }
}

/** Lists the broker connections the server-side bridge exposes. */
export async function loadConnections(baseUrl: string, auth: ResolvedAuth): Promise<BrokerConnection[]> {
  const url = withAuthQuery(`${proxyBaseUrlFor(baseUrl)}/connections`, auth)
  const response = await fetch(url, {
    headers: { accept: 'application/json', ...auth.headers },
  })
  if (!response.ok) {
    throw new Error(
      `Failed to list broker connections (${await problemText(response)}). ` +
        'Ensure the target app calls AddScalarBrokerBridge(...) and MapScalarBrokerAssets().',
    )
  }
  return (await response.json()) as BrokerConnection[]
}

/** Publishes one message through the bridge. */
export async function publish(
  baseUrl: string,
  auth: ResolvedAuth,
  connection: string,
  channel: string,
  payload: string,
  key?: string,
  headers?: Record<string, string>,
): Promise<BrokerPublishReceipt> {
  const url = withAuthQuery(`${proxyBaseUrlFor(baseUrl)}/publish`, auth)
  const response = await fetch(url, {
    method: 'POST',
    headers: { 'content-type': 'application/json', accept: 'application/json', ...auth.headers },
    body: JSON.stringify({ connection, channel, payload, key, headers }),
  })
  if (!response.ok) {
    throw new Error(`Publish failed: ${await problemText(response)}`)
  }
  return (await response.json()) as BrokerPublishReceipt
}

/**
 * Tails a channel, yielding each message as it arrives.
 *
 * The response is Server-Sent Events read through `fetch` + `ReadableStream` rather than
 * `EventSource`, because `EventSource` cannot send the Authorization header the proxy sits behind.
 */
export async function* tail(
  baseUrl: string,
  auth: ResolvedAuth,
  connection: string,
  channel: string,
  signal: AbortSignal,
): AsyncGenerator<BrokerTailMessage> {
  const url = withAuthQuery(
    `${proxyBaseUrlFor(baseUrl)}/tail?connection=${encodeURIComponent(connection)}&channel=${encodeURIComponent(channel)}`,
    auth,
  )
  const response = await fetch(url, {
    headers: { accept: 'text/event-stream', ...auth.headers },
    signal,
  })
  if (!response.ok || !response.body) {
    throw new Error(`Tail failed: ${await problemText(response)}`)
  }

  const reader = response.body.pipeThrough(new TextDecoderStream()).getReader()
  let buffer = ''
  try {
    while (true) {
      const { done, value } = await reader.read()
      if (done) {
        return
      }
      buffer += value
      // SSE frames are separated by a blank line; anything after the last one is a partial frame,
      // so it stays in the buffer until the rest of it arrives.
      const frames = buffer.split('\n\n')
      buffer = frames.pop() ?? ''
      for (const frame of frames) {
        const data = frame
          .split('\n')
          .filter((line) => line.startsWith('data:'))
          .map((line) => line.slice(5).trimStart())
          .join('\n')
        if (data) {
          yield JSON.parse(data) as BrokerTailMessage
        }
      }
    }
  } finally {
    reader.cancel().catch(() => undefined)
  }
}
