<script setup lang="ts">
import {
  HttpTransportType,
  type HubConnection,
  HubConnectionBuilder,
  HubConnectionState,
  LogLevel,
} from '@microsoft/signalr'
import { computed, onBeforeUnmount, reactive, ref, watch } from 'vue'
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

const selectedHub = computed(() => hubs.value.find((hub) => hubKey(hub) === selectedKey.value) ?? null)
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

  const flag = transportFlag()
  connection = new HubConnectionBuilder()
    .withUrl(hubUrl.value, flag !== undefined ? { transport: flag } : {})
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
    addLog('sys', `Connecting to ${hubUrl.value}…`)
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

async function init() {
  loading.value = true
  hubs.value = await loadSignalRHubs(resolveDocuments(props.options, props.documents))
  if (hubs.value.length > 0) {
    selectedKey.value = hubKey(hubs.value[0])
  }
  loading.value = false
}

void init()
onBeforeUnmount(() => void disconnect())
</script>

<template>
  <section class="bsr">
    <header class="bsr__head">
      <h2 class="bsr__title">SignalR</h2>
      <span class="bsr__pill" :data-state="stateLabel.toLowerCase()">{{ stateLabel }}</span>
    </header>

    <p v-if="loading" class="bsr__muted">Loading SignalR hubs…</p>
    <p v-else-if="hubs.length === 0" class="bsr__muted">
      No SignalR hubs found in the AsyncAPI document(s).
    </p>

    <template v-else>
      <!-- Connection bar -->
      <div class="bsr__card bsr__conn">
        <div class="bsr__conn-row">
          <label class="bsr__field">
            <span>Hub</span>
            <select v-model="selectedKey">
              <option v-for="hub in hubs" :key="hubKey(hub)" :value="hubKey(hub)">
                {{ hub.channelName }} ({{ hub.hubPath }})
              </option>
            </select>
          </label>
          <label class="bsr__field bsr__field--grow">
            <span>Base URL</span>
            <input v-model="baseUrlOverride" type="text" spellcheck="false" />
          </label>
          <label class="bsr__field">
            <span>Transport</span>
            <select v-model="transport">
              <option value="auto">Auto</option>
              <option value="webSockets">WebSockets</option>
              <option value="serverSentEvents">Server-Sent Events</option>
              <option value="longPolling">Long Polling</option>
            </select>
          </label>
        </div>
        <div class="bsr__conn-row bsr__conn-row--foot">
          <code class="bsr__url">{{ hubUrl }}</code>
          <button v-if="!isConnected" type="button" class="bsr__btn bsr__btn--primary" @click="connect">Connect</button>
          <button v-else type="button" class="bsr__btn" @click="disconnect">Disconnect</button>
        </div>
      </div>

      <!-- Two columns on wide screens (interactive methods | events + log); stacked on narrow. -->
      <div class="bsr__grid">
      <!-- Left column: client -> server methods. Pick one, edit the prefilled example, send. -->
      <div class="bsr__col">
      <h3 class="bsr__section">Methods</h3>
      <p v-if="clientToServer.length === 0" class="bsr__muted">No invocable hub methods.</p>
      <article v-else class="bsr__card bsr__op">
        <div class="bsr__conn-row">
          <label class="bsr__field bsr__field--grow">
            <span>Method</span>
            <select v-model="selectedMethodId">
              <option v-for="op in clientToServer" :key="op.id" :value="op.id">
                {{ op.target }} · {{ methodLabel(op) }}
              </option>
            </select>
          </label>
          <label v-if="selectedMethod && selectedMethod.messages.length > 1" class="bsr__field bsr__field--grow">
            <span>Message</span>
            <select
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
          <div class="bsr__op-head bsr__op-head--spaced">
            <span class="bsr__method" :data-kind="selectedMethod.callType ?? 'invoke'">{{ methodLabel(selectedMethod) }}</span>
            <span class="bsr__op-target">{{ selectedMethod.target }}</span>
            <span class="bsr__hint">client → server</span>
          </div>
          <p v-if="selectedMethod.summary" class="bsr__muted bsr__op-summary">{{ selectedMethod.summary }}</p>

          <div class="bsr__code-head">
            <span class="bsr__hint">Arguments (JSON array)</span>
            <button
              type="button"
              class="bsr__link"
              :disabled="selectedMethod.messages.length === 0"
              @click="applyExample(selectedMethod, selectedMessageIndex[opKey(selectedMethod)] ?? 0)"
            >
              ↺ Reset to example
            </button>
          </div>
          <textarea
            v-model="invokeArgs[opKey(selectedMethod)]"
            class="bsr__code"
            rows="6"
            spellcheck="false"
            placeholder='Arguments as a JSON array, e.g. ["alice", "hello"]'
          />
          <div class="bsr__op-foot">
            <span class="bsr__hint">{{ hubUrl }}</span>
            <button type="button" class="bsr__btn bsr__btn--primary" :disabled="!isConnected" @click="testRequest(selectedMethod)">
              ▶ Test Request
            </button>
          </div>
          <pre v-if="results[opKey(selectedMethod)]" class="bsr__result" :data-ok="results[opKey(selectedMethod)].ok">{{ results[opKey(selectedMethod)].text }}</pre>
        </template>
      </article>
      </div>

      <!-- Right column: the live message log (the useful part) above the events reference. -->
      <div class="bsr__col">
      <!-- Live message log -->
      <h3 class="bsr__section">Messages</h3>
      <ul class="bsr__log">
        <li v-for="(entry, index) in log" :key="index" :class="`bsr__log-${entry.dir}`">
          <span class="bsr__time">{{ entry.time }}</span>
          <span class="bsr__badge">{{ entry.dir }}</span>
          <span>{{ entry.text }}</span>
        </li>
        <li v-if="log.length === 0" class="bsr__muted">No messages yet.</li>
      </ul>

      <!-- Server -> client events reference -->
      <h3 class="bsr__section">Events</h3>
      <p v-if="serverToClient.length === 0" class="bsr__muted">No server-to-client events.</p>
      <article v-for="op in serverToClient" :key="op.id" class="bsr__card bsr__op">
        <div class="bsr__op-head">
          <span class="bsr__method" data-kind="event">EVENT</span>
          <span class="bsr__op-target">{{ op.target }}</span>
          <span class="bsr__hint">server → client</span>
        </div>
        <p v-if="op.summary" class="bsr__muted bsr__op-summary">{{ op.summary }}</p>
        <pre v-if="op.messages.length > 0" class="bsr__result bsr__result--sample">{{ op.messages[0].example }}</pre>
      </article>
      </div>
      </div>
    </template>
  </section>
</template>

<style scoped>
.bsr {
  font-family: var(--scalar-font, system-ui, sans-serif);
  color: var(--scalar-color-1, inherit);
  font-size: var(--scalar-font-size-3, 0.9rem);
  /* Render as its own Scalar-style section: a clear top divider, generous breathing room above,
     and horizontal insets so it lines up with the operations/models instead of sitting flush
     against the viewport edge. `--bsr-content-padding` lets hosts fine-tune the inset. */
  padding: 2.5rem var(--bsr-content-padding, var(--scalar-content-padding, 24px)) 3rem;
  margin-top: 2rem;
  border-top: 1px solid var(--scalar-border-color, #e3e3e3);
  max-width: var(--bsr-content-max-width, none);
}

.bsr__head {
  display: flex;
  align-items: center;
  gap: 0.75rem;
  margin-bottom: 1.25rem;
}

/* Two-column layout on wide screens (methods | events + log); single column when narrow.
   The connection bar stays full width above this grid. */
.bsr__grid {
  display: grid;
  grid-template-columns: 1fr;
  gap: 0 2rem;
  align-items: start;
}

.bsr__col {
  min-width: 0;
}

@media (min-width: 60rem) {
  .bsr__grid {
    grid-template-columns: minmax(0, 1fr) minmax(0, 1fr);
  }
}

.bsr__title {
  margin: 0;
  font-size: 1.25rem;
}

.bsr__pill {
  font-size: 0.7rem;
  text-transform: uppercase;
  letter-spacing: 0.03em;
  padding: 0.15rem 0.5rem;
  border-radius: 999px;
  background: var(--scalar-background-2, #eee);
  color: var(--scalar-color-2, #555);
}

.bsr__pill[data-state='connected'] {
  background: color-mix(in srgb, #2e7d32 18%, transparent);
  color: #2e7d32;
}

.bsr__card {
  border: 1px solid var(--scalar-border-color, #e3e3e3);
  border-radius: var(--scalar-radius, 8px);
  background: var(--scalar-background-1, transparent);
  padding: 0.85rem;
  margin-bottom: 0.85rem;
}

.bsr__conn-row {
  display: flex;
  flex-wrap: wrap;
  gap: 0.75rem;
  align-items: flex-end;
}

.bsr__conn-row--foot {
  margin-top: 0.75rem;
  align-items: center;
}

.bsr__field {
  display: flex;
  flex-direction: column;
  gap: 0.25rem;
}

.bsr__field > span {
  font-size: 0.72rem;
  text-transform: uppercase;
  letter-spacing: 0.03em;
  color: var(--scalar-color-3, #777);
}

.bsr__field--grow {
  flex: 1;
  min-width: 12rem;
}

.bsr__field input,
.bsr__field select {
  padding: 0.35rem 0.5rem;
  border: 1px solid var(--scalar-border-color, #ddd);
  border-radius: var(--scalar-radius, 6px);
  background: var(--scalar-background-2, #fff);
  color: inherit;
  font: inherit;
}

.bsr__section {
  margin: 1.25rem 0 0.5rem;
  font-size: 0.95rem;
}

.bsr__op-head {
  display: flex;
  align-items: center;
  gap: 0.6rem;
}

.bsr__op-head--spaced {
  margin-top: 0.85rem;
}

.bsr__code-head {
  display: flex;
  align-items: center;
  justify-content: space-between;
  margin-top: 0.6rem;
}

.bsr__link {
  font: inherit;
  font-size: 0.72rem;
  background: none;
  border: 0;
  padding: 0;
  cursor: pointer;
  color: var(--scalar-color-blue, #2563eb);
}

.bsr__link:disabled {
  opacity: 0.5;
  cursor: not-allowed;
}

.bsr__result--sample {
  border-left-color: var(--scalar-color-3, #888);
  margin-top: 0.6rem;
}

.bsr__method {
  font-family: var(--scalar-font-code, monospace);
  font-size: 0.7rem;
  font-weight: 700;
  letter-spacing: 0.03em;
  padding: 0.15rem 0.45rem;
  border-radius: var(--scalar-radius, 5px);
  background: color-mix(in srgb, var(--scalar-color-blue, #2563eb) 16%, transparent);
  color: var(--scalar-color-blue, #2563eb);
}

.bsr__method[data-kind='send'] {
  background: color-mix(in srgb, var(--scalar-color-orange, #d97706) 16%, transparent);
  color: var(--scalar-color-orange, #d97706);
}

.bsr__method[data-kind='event'] {
  background: color-mix(in srgb, var(--scalar-color-green, #16a34a) 16%, transparent);
  color: var(--scalar-color-green, #16a34a);
}

.bsr__op-target {
  font-family: var(--scalar-font-code, monospace);
  font-weight: 600;
}

.bsr__op-summary {
  margin: 0.4rem 0 0;
}

.bsr__code {
  width: 100%;
  margin-top: 0.6rem;
  padding: 0.6rem;
  border: 1px solid var(--scalar-border-color, #ddd);
  border-radius: var(--scalar-radius, 6px);
  background: var(--scalar-background-2, #1e1e1e);
  color: inherit;
  font-family: var(--scalar-font-code, monospace);
  font-size: 0.82rem;
  resize: vertical;
  box-sizing: border-box;
}

.bsr__op-foot {
  display: flex;
  align-items: center;
  justify-content: space-between;
  margin-top: 0.6rem;
}

.bsr__hint {
  font-size: 0.72rem;
  color: var(--scalar-color-3, #888);
}

.bsr__url {
  flex: 1;
  min-width: 10rem;
  font-family: var(--scalar-font-code, monospace);
  font-size: 0.8rem;
  color: var(--scalar-color-2, #555);
  overflow-wrap: anywhere;
}

.bsr__btn {
  font: inherit;
  font-weight: 600;
  padding: 0.4rem 0.8rem;
  border-radius: var(--scalar-radius, 6px);
  border: 1px solid var(--scalar-border-color, #ccc);
  background: var(--scalar-background-2, #f4f4f4);
  color: inherit;
  cursor: pointer;
}

.bsr__btn--primary {
  background: var(--scalar-button-1, #111);
  color: var(--scalar-button-1-color, #fff);
  border-color: transparent;
}

.bsr__btn:disabled {
  opacity: 0.5;
  cursor: not-allowed;
}

.bsr__result {
  margin: 0.6rem 0 0;
  padding: 0.6rem;
  border-radius: var(--scalar-radius, 6px);
  background: var(--scalar-background-2, #f6f6f6);
  border-left: 3px solid var(--scalar-color-green, #16a34a);
  font-family: var(--scalar-font-code, monospace);
  font-size: 0.8rem;
  white-space: pre-wrap;
  overflow-wrap: anywhere;
}

.bsr__result[data-ok='false'] {
  border-left-color: var(--scalar-color-red, #dc2626);
}

.bsr__log {
  list-style: none;
  margin: 0;
  padding: 0.5rem;
  max-height: 16rem;
  overflow: auto;
  border: 1px solid var(--scalar-border-color, #e3e3e3);
  border-radius: var(--scalar-radius, 8px);
  background: var(--scalar-background-1, transparent);
  font-family: var(--scalar-font-code, monospace);
  font-size: 0.78rem;
}

.bsr__log li {
  display: flex;
  gap: 0.5rem;
  padding: 0.12rem 0;
}

.bsr__time {
  opacity: 0.55;
}

.bsr__badge {
  text-transform: uppercase;
  font-size: 0.62rem;
  align-self: center;
  border-radius: 3px;
  padding: 0 0.25rem;
  background: var(--scalar-background-2, #eee);
}

.bsr__log-in .bsr__badge {
  background: color-mix(in srgb, var(--scalar-color-blue, #2563eb) 20%, transparent);
}

.bsr__log-out .bsr__badge {
  background: color-mix(in srgb, var(--scalar-color-orange, #d97706) 20%, transparent);
}

.bsr__muted {
  color: var(--scalar-color-3, #888);
}
</style>
