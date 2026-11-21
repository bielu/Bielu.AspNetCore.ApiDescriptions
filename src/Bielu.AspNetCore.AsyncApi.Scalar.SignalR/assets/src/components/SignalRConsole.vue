<script setup lang="ts">
import {
  HttpTransportType,
  type HubConnection,
  type IHttpConnectionOptions,
  HubConnectionBuilder,
  HubConnectionState,
  LogLevel,
} from '@microsoft/signalr'
import { computed, onBeforeUnmount, onMounted, reactive, ref, watch } from 'vue'
import { getAuthState, resolveSignalRAuth } from '../auth'
import { resolveDocuments } from '../discovery'
import { loadSignalRHubs } from '../signalr-bindings'
import type { SignalRDocumentRef, SignalRHubModel, SignalROperationModel } from '../types'

// `options` is the Scalar configuration Scalar binds to the element; `documents` is an optional
// explicit override. Documents are otherwise auto-discovered from `options.sources`.
const props = defineProps<{ options?: Record<string, any>; documents?: SignalRDocumentRef[] }>()

type TransportChoice = 'auto' | 'webSockets' | 'serverSentEvents' | 'longPolling'
type LogEntry = { time: string; dir: 'in' | 'out' | 'sys'; text: string }

const loading = ref(true)
const hubs = ref<SignalRHubModel[]>([])
// The console's own root element, used to discover which Scalar document it is rendered under.
const rootEl = ref<HTMLElement | null>(null)
// Scalar's active document slug (null on stock Scalar / standalone use, where the picker is shown).
const activeDocumentSlug = ref<string | null>(null)
const selectedDocumentName = ref<string | null>(null)
const selectedKey = ref<string | null>(null)
const baseUrlOverride = ref('')
const transport = ref<TransportChoice>('auto')
const state = ref<HubConnectionState>(HubConnectionState.Disconnected)
const log = reactive<LogEntry[]>([])
// Per-operation state survives hub switches, so it is keyed by hub + operation id (operations from
// different hubs/documents can share an `op.id`, which would otherwise overwrite each other).
const invokeArgs = reactive<Record<string, string>>({})
const results = reactive<Record<string, { ok: boolean; text: string }>>({})
// Which method is selected per hub (keyed by hubKey), and which message of an op is the active example.
const selectedMethodIds = reactive<Record<string, string>>({})
const selectedMessageIndex = reactive<Record<string, number>>({})

let connection: HubConnection | null = null

const hubKey = (hub: SignalRHubModel) => `${hub.documentName}:${hub.channelName}`
// A hub-scoped key for an operation. All displayed operations belong to the selected hub, so the
// selected hub key uniquely scopes them.
const opKey = (op: SignalROperationModel) => `${selectedKey.value}:${op.id}`

const uniqueDocuments = computed(() => {
  const seen = new Set<string>()
  const docs: string[] = []
  for (const h of hubs.value) {
    if (!seen.has(h.documentName)) {
      seen.add(h.documentName)
      docs.push(h.documentName)
    }
  }
  return docs
})

// Scope the visible hubs to Scalar's active document when known (so the console reflects the
// document the user is viewing), otherwise to the manually picked document. Falls back to all hubs
// only when neither is set.
const scopedDocumentName = computed(() => activeDocumentSlug.value ?? selectedDocumentName.value)
const filteredHubs = computed(() =>
  scopedDocumentName.value
    ? hubs.value.filter((h) => h.documentName === scopedDocumentName.value)
    : hubs.value,
)

// The document picker only makes sense when the console spans several documents itself. Once it is
// scoped to Scalar's active document, the active document is authoritative, so the picker is hidden.
const showDocumentPicker = computed(() => !activeDocumentSlug.value && uniqueDocuments.value.length > 1)

const selectedHub = computed(() => filteredHubs.value.find((hub) => hubKey(hub) === selectedKey.value) ?? null)
const clientToServer = computed(() => selectedHub.value?.operations.filter((op) => op.direction === 'clientToServer') ?? [])
const serverToClient = computed(() => selectedHub.value?.operations.filter((op) => op.direction === 'serverToClient') ?? [])
// The selected method id for the current hub, persisted per hub so switching hubs keeps each choice.
const selectedMethodId = computed<string | null>({
  get: () => (selectedKey.value ? selectedMethodIds[selectedKey.value] ?? null : null),
  set: (value) => {
    if (selectedKey.value && value) {
      selectedMethodIds[selectedKey.value] = value
    }
  },
})
const selectedMethod = computed(() => clientToServer.value.find((op) => op.id === selectedMethodId.value) ?? null)
const isConnected = computed(() => state.value === HubConnectionState.Connected)
// Full class names, so Tailwind finds them when it scans this file for the utilities to generate.
const logBadge: Record<LogEntry['dir'], string> = {
  in: 'bg-blue/20',
  out: 'bg-orange/20',
  sys: 'bg-b-2',
}

const stateLabel = computed(() => HubConnectionState[state.value] ?? 'Disconnected')

const hubUrl = computed(() => {
  const hub = selectedHub.value
  if (!hub) {
    return ''
  }
  const base = (baseUrlOverride.value || hub.baseUrl || '').replace(/\/+$/, '')
  const path = hub.hubPath.startsWith('/') ? hub.hubPath : `/${hub.hubPath}`
  return `${base}${path}`
})

function addLog(dir: LogEntry['dir'], text: string) {
  log.unshift({ time: new Date().toLocaleTimeString(), dir, text })
  if (log.length > 200) {
    log.pop()
  }
}

function transportFlag(): HttpTransportType | undefined {
  switch (transport.value) {
    case 'webSockets':
      return HttpTransportType.WebSockets
    case 'serverSentEvents':
      return HttpTransportType.ServerSentEvents
    case 'longPolling':
      return HttpTransportType.LongPolling
    default:
      return undefined
  }
}

/**
 * Parse the editor contents into a positional argument array. Throws on invalid JSON so malformed
 * input surfaces as a validation error instead of being silently sent as a raw string argument.
 */
function parseArgs(raw: string): unknown[] {
  const text = (raw ?? '').trim()
  if (!text) {
    return []
  }
  const parsed = JSON.parse(text)
  return Array.isArray(parsed) ? parsed : [parsed]
}

async function disconnect() {
  if (connection) {
    try {
      await connection.stop()
    } catch {
      // ignore stop errors
    }
    connection = null
  }
  state.value = HubConnectionState.Disconnected
}

async function connect() {
  const hub = selectedHub.value
  if (!hub) {
    return
  }
  await disconnect()

  // Resolve Scalar's current auth state → no-op on stock Scalar or when no scheme is selected.
  const authState = getAuthState()
  const authResult = resolveSignalRAuth(hub.documentName, hub.securitySchemes, authState)
  for (const warn of authResult.warnings) {
    addLog('sys', `Auth warning: ${warn}`)
  }

  // Warn when the hub declares security schemes but no credentials were resolved.
  const hasSchemes = hub.securitySchemes && Object.keys(hub.securitySchemes).length > 0
  const resolvedCredentials =
    Object.keys(authResult.queryParams).length > 0 || !!authResult.accessToken
  if (hasSchemes && !resolvedCredentials) {
    if (!authState) {
      addLog(
        'sys',
        'Auth: no Scalar auth state available — this Scalar version may predate the plugin auth-state API (Scalar ≥ 2.16.12).',
      )
    } else {
      addLog(
        'sys',
        `Auth: no credentials resolved for document "${hub.documentName}" — select a security scheme in the Authentication panel and enter your credentials.`,
      )
    }
  }

  // Append any resolved query params (e.g. api_key) to the hub URL.
  let effectiveUrl = hubUrl.value
  if (Object.keys(authResult.queryParams).length > 0) {
    const qs = new URLSearchParams(authResult.queryParams).toString()
    effectiveUrl += (effectiveUrl.includes('?') ? '&' : '?') + qs
  }

  const flag = transportFlag()
  const withUrlOptions: IHttpConnectionOptions = flag !== undefined ? { transport: flag } : {}
  if (authResult.accessToken) {
    const token = authResult.accessToken
    withUrlOptions.accessTokenFactory = () => token
  }

  connection = new HubConnectionBuilder()
    .withUrl(effectiveUrl, withUrlOptions)
    .configureLogging(LogLevel.Warning)
    .withAutomaticReconnect()
    .build()

  for (const op of serverToClient.value) {
    connection.on(op.target, (...args: unknown[]) => {
      addLog('in', `${op.target}(${args.map((arg) => JSON.stringify(arg)).join(', ')})`)
    })
  }

  connection.onclose((error) => {
    state.value = HubConnectionState.Disconnected
    addLog('sys', error ? `Disconnected: ${error.message}` : 'Disconnected')
  })
  connection.onreconnecting(() => {
    state.value = HubConnectionState.Reconnecting
    addLog('sys', 'Reconnecting…')
  })
  connection.onreconnected(() => {
    state.value = HubConnectionState.Connected
    addLog('sys', 'Reconnected')
  })

  try {
    addLog('sys', `Connecting to ${effectiveUrl}…`)
    await connection.start()
    state.value = HubConnectionState.Connected
    addLog('sys', 'Connected')
  } catch (error: any) {
    state.value = HubConnectionState.Disconnected
    addLog('sys', `Connection failed: ${error?.message ?? error}`)
  }
}

/** Drain a SignalR stream into an array, logging each item as it arrives. */
function streamAll(target: string, args: unknown[]): Promise<unknown[]> {
  return new Promise((resolve, reject) => {
    const items: unknown[] = []
    connection!.stream(target, ...args).subscribe({
      next: (item: unknown) => {
        items.push(item)
        addLog('in', `← ${JSON.stringify(item)}`)
      },
      error: (error: unknown) => reject(error),
      complete: () => resolve(items),
    })
  })
}

async function testRequest(op: SignalROperationModel) {
  const key = opKey(op)
  if (!connection || !isConnected.value) {
    results[key] = { ok: false, text: 'Not connected — press Connect first.' }
    return
  }
  let args: unknown[]
  try {
    args = parseArgs(invokeArgs[key])
  } catch (error: any) {
    results[key] = { ok: false, text: `Invalid JSON arguments: ${error?.message ?? error}` }
    return
  }
  addLog('out', `${op.target}(${args.map((arg) => JSON.stringify(arg)).join(', ')})`)
  try {
    if (op.callType === 'send') {
      await connection.send(op.target, ...args)
      results[key] = { ok: true, text: 'Sent (fire-and-forget, no response).' }
    } else if (op.callType === 'streamInvocation') {
      const items = await streamAll(op.target, args)
      results[key] = { ok: true, text: `Stream completed: ${items.length} item(s).\n${JSON.stringify(items, null, 2)}` }
    } else {
      const result = await connection.invoke(op.target, ...args)
      const text = result === undefined ? 'Completed (no return value).' : JSON.stringify(result, null, 2)
      results[key] = { ok: true, text }
      if (result !== undefined) {
        addLog('in', `← ${JSON.stringify(result)}`)
      }
    }
  } catch (error: any) {
    results[key] = { ok: false, text: `Error: ${error?.message ?? error}` }
    addLog('sys', `Invoke failed: ${error?.message ?? error}`)
  }
}

function methodLabel(op: SignalROperationModel): string {
  return (op.callType ?? 'invoke').toUpperCase()
}

/** The example string for a given message of an operation (falls back to an empty array). */
function exampleFor(op: SignalROperationModel | null, index: number): string {
  return op?.messages?.[index]?.example ?? '[]'
}

/** Select a message and (re)fill the arguments editor with its generated example. */
function applyExample(op: SignalROperationModel, index: number) {
  const key = opKey(op)
  selectedMessageIndex[key] = index
  invokeArgs[key] = exampleFor(op, index)
}

/** Prefill an operation's editor with its first example, but only if it is still untouched. */
function ensurePrefilled(op: SignalROperationModel) {
  const key = opKey(op)
  if (invokeArgs[key] === undefined) {
    applyExample(op, selectedMessageIndex[key] ?? 0)
  }
}

// Keep the selected hub within the visible (document-scoped) set. Fires when the document changes
// (picker or active-document detection) and when hubs finish loading.
watch(
  filteredHubs,
  (list) => {
    if (!list.some((hub) => hubKey(hub) === selectedKey.value)) {
      selectedKey.value = list[0] ? hubKey(list[0]) : null
    }
  },
  { immediate: true },
)

watch(selectedHub, (hub) => {
  baseUrlOverride.value = hub?.baseUrl ?? ''
  void disconnect()
})

// Editing the Base URL or Transport changes the endpoint, so drop any live connection — the next
// Connect rebuilds it from the current hubUrl/transport settings.
watch([baseUrlOverride, transport], () => {
  void disconnect()
})

// When the available methods change (e.g. a different hub), select the first one and prefill it.
watch(
  clientToServer,
  (ops) => {
    if (!ops.some((op) => op.id === selectedMethodId.value)) {
      selectedMethodId.value = ops[0]?.id ?? null
    }
    ops.forEach(ensurePrefilled)
  },
  { immediate: true },
)

// Selecting a different method prefills its editor on first visit.
watch(selectedMethod, (op) => {
  if (op) {
    ensurePrefilled(op)
  }
})

/**
 * Discover Scalar's active document from the plugin-view wrapper this console is rendered inside.
 * Scalar renders each `content.end` plugin view under a `<div id="{documentSlug}/plugin-view/…">`
 * scoped to the active document, so the slug is the id prefix. Returns null when the console is not
 * inside such a wrapper (stock Scalar / standalone), leaving the multi-document picker in place.
 */
function detectActiveDocumentSlug(): string | null {
  const wrapper = rootEl.value?.closest('[id*="/plugin-view/"]')
  const slug = wrapper?.id.split('/plugin-view/')[0]
  return slug || null
}

async function init() {
  loading.value = true
  hubs.value = await loadSignalRHubs(resolveDocuments(props.options, props.documents))
  // Seed a default document for the picker; the active-document scope (when detected) overrides it.
  if (hubs.value.length > 0 && !selectedDocumentName.value) {
    selectedDocumentName.value = hubs.value[0].documentName
  }
  loading.value = false
}

void init()
onMounted(() => {
  activeDocumentSlug.value = detectActiveDocumentSlug()
})
onBeforeUnmount(() => void disconnect())
</script>

<template>
  <section
    ref="rootEl"
    class="bsr mt-8 max-w-(--bsr-content-max-width,none) border-t border-border px-(--bsr-content-padding,var(--scalar-content-padding,24px)) pt-10 pb-12 font-sans text-base text-c-1"
  >
    <header class="mb-5 flex items-center gap-3">
      <h2 class="m-0 text-xl">SignalR</h2>
      <span
        class="bielu-pill bg-b-2 text-c-2 data-[state=connected]:bg-green/18 data-[state=connected]:text-green"
        :data-state="stateLabel.toLowerCase()"
      >{{ stateLabel }}</span>
    </header>

    <p v-if="loading" class="text-c-3">Loading SignalR hubs…</p>
    <p v-else-if="filteredHubs.length === 0" class="text-c-3">
      No SignalR hubs found in {{ activeDocumentSlug ? 'this document' : 'the AsyncAPI document(s)' }}.
    </p>

    <template v-else>
      <!-- Connection bar -->
      <div class="bielu-card mb-3.5">
        <div class="flex flex-wrap items-end gap-3">
          <label v-if="showDocumentPicker" class="flex flex-col gap-1">
            <span class="bielu-caption">Document</span>
            <select class="bielu-input" v-model="selectedDocumentName">
              <option v-for="doc in uniqueDocuments" :key="doc" :value="doc">{{ doc }}</option>
            </select>
          </label>
          <label class="flex flex-col gap-1">
            <span class="bielu-caption">Hub</span>
            <select class="bielu-input" v-model="selectedKey">
              <option v-for="hub in filteredHubs" :key="hubKey(hub)" :value="hubKey(hub)">
                {{ hub.channelName }} ({{ hub.hubPath }})
              </option>
            </select>
          </label>
          <label class="flex min-w-48 flex-1 flex-col gap-1">
            <span class="bielu-caption">Base URL</span>
            <input v-model="baseUrlOverride" class="bielu-input" type="text" spellcheck="false" />
          </label>
          <label class="flex flex-col gap-1">
            <span class="bielu-caption">Transport</span>
            <select class="bielu-input" v-model="transport">
              <option value="auto">Auto</option>
              <option value="webSockets">WebSockets</option>
              <option value="serverSentEvents">Server-Sent Events</option>
              <option value="longPolling">Long Polling</option>
            </select>
          </label>
        </div>
        <div class="mt-3 flex flex-wrap items-center gap-3">
          <code class="min-w-40 flex-1 font-code text-sm wrap-anywhere text-c-2">{{ hubUrl }}</code>
          <button v-if="!isConnected" type="button" class="bielu-btn bielu-btn-primary" @click="connect">Connect</button>
          <button v-else type="button" class="bielu-btn" @click="disconnect">Disconnect</button>
        </div>
      </div>

      <!-- Two columns on wide screens (interactive methods | events + log); stacked on narrow. -->
      <div class="grid grid-cols-1 items-start gap-x-8 min-[60rem]:grid-cols-2">
      <!-- Left column: client -> server methods. Pick one, edit the prefilled example, send. -->
      <div class="min-w-0">
      <h3 class="mt-5 mb-2 text-base font-medium">Methods</h3>
      <p v-if="clientToServer.length === 0" class="text-c-3">No invocable hub methods.</p>
      <article v-else class="bielu-card mb-3.5">
        <div class="flex flex-wrap items-end gap-3">
          <label class="flex min-w-48 flex-1 flex-col gap-1">
            <span class="bielu-caption">Method</span>
            <select class="bielu-input" v-model="selectedMethodId">
              <option v-for="op in clientToServer" :key="op.id" :value="op.id">
                {{ op.target }} · {{ methodLabel(op) }}
              </option>
            </select>
          </label>
          <label v-if="selectedMethod && selectedMethod.messages.length > 1" class="flex min-w-48 flex-1 flex-col gap-1">
            <span class="bielu-caption">Message</span>
            <select
              class="bielu-input"
              :value="selectedMessageIndex[opKey(selectedMethod)] ?? 0"
              @change="applyExample(selectedMethod, Number(($event.target as HTMLSelectElement).value))"
            >
              <option v-for="(message, index) in selectedMethod.messages" :key="message.name" :value="index">
                {{ message.title || message.name }}
              </option>
            </select>
          </label>
        </div>

        <template v-if="selectedMethod">
          <div class="mt-3.5 flex items-center gap-2.5">
            <span
              class="bielu-tag bg-blue/16 text-blue data-[kind=send]:bg-orange/16 data-[kind=send]:text-orange"
              :data-kind="selectedMethod.callType ?? 'invoke'"
            >{{ methodLabel(selectedMethod) }}</span>
            <span class="font-code font-medium">{{ selectedMethod.target }}</span>
            <span class="text-xxs text-c-3">client → server</span>
          </div>
          <p v-if="selectedMethod.summary" class="mt-1.5 mb-0 text-c-3">{{ selectedMethod.summary }}</p>

          <div class="mt-2.5 flex items-center justify-between">
            <span class="text-xxs text-c-3">Arguments (JSON array)</span>
            <button
              type="button"
              class="cursor-pointer border-0 bg-transparent p-0 text-xxs text-blue disabled:cursor-not-allowed disabled:opacity-50"
              :disabled="selectedMethod.messages.length === 0"
              @click="applyExample(selectedMethod, selectedMessageIndex[opKey(selectedMethod)] ?? 0)"
            >
              ↺ Reset to example
            </button>
          </div>
          <textarea
            v-model="invokeArgs[opKey(selectedMethod)]"
            class="bielu-code-editor mt-2.5"
            rows="6"
            spellcheck="false"
            placeholder='Arguments as a JSON array, e.g. ["alice", "hello"]'
          />
          <div class="mt-2.5 flex items-center justify-between">
            <span class="text-xxs text-c-3">{{ hubUrl }}</span>
            <button type="button" class="bielu-btn bielu-btn-primary" :disabled="!isConnected" @click="testRequest(selectedMethod)">
              ▶ Test Request
            </button>
          </div>
          <pre v-if="results[opKey(selectedMethod)]" class="bielu-result mt-2.5 border-l-green data-[ok=false]:border-l-red" :data-ok="results[opKey(selectedMethod)].ok">{{ results[opKey(selectedMethod)].text }}</pre>
        </template>
      </article>
      </div>

      <!-- Right column: the live message log (the useful part) above the events reference. -->
      <div class="min-w-0">
      <!-- Live message log -->
      <h3 class="mt-5 mb-2 text-base font-medium">Messages</h3>
      <ul class="bielu-log">
        <li v-for="(entry, index) in log" :key="index" class="flex gap-2 py-0.5">
          <span class="opacity-55">{{ entry.time }}</span>
          <span class="self-center rounded px-1 text-3xs uppercase" :class="logBadge[entry.dir]">{{ entry.dir }}</span>
          <span>{{ entry.text }}</span>
        </li>
        <li v-if="log.length === 0" class="text-c-3">No messages yet.</li>
      </ul>

      <!-- Server -> client events reference -->
      <h3 class="mt-5 mb-2 text-base font-medium">Events</h3>
      <p v-if="serverToClient.length === 0" class="text-c-3">No server-to-client events.</p>
      <article v-for="op in serverToClient" :key="op.id" class="bielu-card mb-3.5">
        <div class="flex items-center gap-2.5">
          <span class="bielu-tag bg-green/16 text-green">EVENT</span>
          <span class="font-code font-medium">{{ op.target }}</span>
          <span class="text-xxs text-c-3">server → client</span>
        </div>
        <p v-if="op.summary" class="mt-1.5 mb-0 text-c-3">{{ op.summary }}</p>
        <pre v-if="op.messages.length > 0" class="bielu-result mt-2.5 border-l-c-3">{{ op.messages[0].example }}</pre>
      </article>
      </div>
      </div>
    </template>
  </section>
</template>

<!-- Not scoped: the utilities are scoped to the console element instead (see ../style.css). -->
<style src="../style.css"></style>
