<script setup lang="ts">
import {
  HttpTransportType,
  type HubConnection,
  HubConnectionBuilder,
  HubConnectionState,
  LogLevel,
} from '@microsoft/signalr'
import { computed, onBeforeUnmount, reactive, ref, watch } from 'vue'
import { loadSignalRHubs } from '../signalr-bindings'
import type { SignalRHubModel, SignalROperationModel, SignalRPluginConfig } from '../types'

const props = defineProps<{ config: SignalRPluginConfig }>()

type TransportChoice = 'auto' | 'webSockets' | 'serverSentEvents' | 'longPolling'
type LogEntry = { time: string; dir: 'in' | 'out' | 'sys'; text: string }

const loading = ref(true)
const hubs = ref<SignalRHubModel[]>([])
const selectedKey = ref<string | null>(null)
const baseUrlOverride = ref('')
const transport = ref<TransportChoice>('auto')
const state = ref<HubConnectionState>(HubConnectionState.Disconnected)
const log = reactive<LogEntry[]>([])
const invokeArgs = reactive<Record<string, string>>({})

let connection: HubConnection | null = null

const hubKey = (hub: SignalRHubModel) => `${hub.documentName}:${hub.channelName}`

const selectedHub = computed(() => hubs.value.find((hub) => hubKey(hub) === selectedKey.value) ?? null)
const clientToServer = computed(() => selectedHub.value?.operations.filter((op) => op.direction === 'clientToServer') ?? [])
const serverToClient = computed(() => selectedHub.value?.operations.filter((op) => op.direction === 'serverToClient') ?? [])
const isConnected = computed(() => state.value === HubConnectionState.Connected)

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

function parseArgs(raw: string): unknown[] {
  const text = (raw ?? '').trim()
  if (!text) {
    return []
  }
  try {
    const parsed = JSON.parse(text)
    return Array.isArray(parsed) ? parsed : [parsed]
  } catch {
    // Not valid JSON — pass the raw text as a single string argument.
    return [text]
  }
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

async function invoke(op: SignalROperationModel) {
  if (!connection || !isConnected.value) {
    addLog('sys', 'Not connected')
    return
  }
  const args = parseArgs(invokeArgs[op.id])
  addLog('out', `${op.target}(${args.map((arg) => JSON.stringify(arg)).join(', ')})`)
  try {
    if (op.callType === 'send') {
      await connection.send(op.target, ...args)
    } else {
      const result = await connection.invoke(op.target, ...args)
      if (result !== undefined) {
        addLog('in', `← ${JSON.stringify(result)}`)
      }
    }
  } catch (error: any) {
    addLog('sys', `Invoke failed: ${error?.message ?? error}`)
  }
}

watch(selectedHub, (hub) => {
  baseUrlOverride.value = hub?.baseUrl ?? ''
  void disconnect()
})

async function init() {
  loading.value = true
  hubs.value = await loadSignalRHubs(props.config?.documents ?? [])
  if (hubs.value.length > 0) {
    selectedKey.value = hubKey(hubs.value[0])
  }
  loading.value = false
}

void init()
onBeforeUnmount(() => void disconnect())
</script>

<template>
  <section class="bielu-signalr">
    <h2 class="bielu-signalr__title">SignalR</h2>

    <p v-if="loading">Loading SignalR hubs…</p>
    <p v-else-if="hubs.length === 0" class="bielu-signalr__muted">
      No SignalR hubs found in the configured AsyncAPI document(s).
    </p>

    <template v-else>
      <div class="bielu-signalr__row">
        <label>
          Hub
          <select v-model="selectedKey">
            <option v-for="hub in hubs" :key="hubKey(hub)" :value="hubKey(hub)">
              {{ hub.channelName }} ({{ hub.hubPath }})
            </option>
          </select>
        </label>
        <label>
          Base URL
          <input v-model="baseUrlOverride" type="text" spellcheck="false" />
        </label>
        <label>
          Transport
          <select v-model="transport">
            <option value="auto">Auto</option>
            <option value="webSockets">WebSockets</option>
            <option value="serverSentEvents">Server-Sent Events</option>
            <option value="longPolling">Long Polling</option>
          </select>
        </label>
      </div>

      <div class="bielu-signalr__row">
        <code class="bielu-signalr__url">{{ hubUrl }}</code>
        <button v-if="!isConnected" type="button" @click="connect">Connect</button>
        <button v-else type="button" @click="disconnect">Disconnect</button>
        <span class="bielu-signalr__state" :data-connected="isConnected">{{ state }}</span>
      </div>

      <div class="bielu-signalr__cols">
        <div>
          <h3>Invoke · client → server</h3>
          <p v-if="clientToServer.length === 0" class="bielu-signalr__muted">No invocable methods.</p>
          <div v-for="op in clientToServer" :key="op.id" class="bielu-signalr__op">
            <div>
              <strong>{{ op.target }}</strong>
              <em v-if="op.callType" class="bielu-signalr__muted">· {{ op.callType }}</em>
            </div>
            <p v-if="op.summary" class="bielu-signalr__muted">{{ op.summary }}</p>
            <textarea
              v-model="invokeArgs[op.id]"
              rows="2"
              placeholder='Arguments as a JSON array, e.g. ["alice", "hello"]'
            />
            <button type="button" :disabled="!isConnected" @click="invoke(op)">Send</button>
          </div>
        </div>

        <div>
          <h3>Subscribe · server → client</h3>
          <p v-if="serverToClient.length === 0" class="bielu-signalr__muted">No server events.</p>
          <ul class="bielu-signalr__events">
            <li v-for="op in serverToClient" :key="op.id">
              <strong>{{ op.target }}</strong>
              <span v-if="op.summary" class="bielu-signalr__muted"> — {{ op.summary }}</span>
            </li>
          </ul>
          <p class="bielu-signalr__muted">Subscribed automatically while connected.</p>
        </div>
      </div>

      <h3>Messages</h3>
      <ul class="bielu-signalr__log">
        <li v-for="(entry, index) in log" :key="index" :class="`bielu-signalr__log-${entry.dir}`">
          <span class="bielu-signalr__time">{{ entry.time }}</span>
          <span class="bielu-signalr__badge">{{ entry.dir }}</span>
          <span>{{ entry.text }}</span>
        </li>
      </ul>
    </template>
  </section>
</template>

<style scoped>
.bielu-signalr {
  padding: 1rem 0;
  font-size: 0.9rem;
}

.bielu-signalr__title {
  margin-bottom: 0.5rem;
}

.bielu-signalr__row {
  display: flex;
  flex-wrap: wrap;
  gap: 0.75rem;
  align-items: flex-end;
  margin-bottom: 0.75rem;
}

.bielu-signalr__row label {
  display: flex;
  flex-direction: column;
  gap: 0.25rem;
}

.bielu-signalr__cols {
  display: grid;
  grid-template-columns: repeat(auto-fit, minmax(260px, 1fr));
  gap: 1.5rem;
  margin: 1rem 0;
}

.bielu-signalr__op {
  border: 1px solid var(--scalar-border-color, #ddd);
  border-radius: 6px;
  padding: 0.5rem;
  margin-bottom: 0.5rem;
}

.bielu-signalr__op textarea {
  width: 100%;
  font-family: var(--scalar-font-code, monospace);
  margin: 0.25rem 0;
}

.bielu-signalr__url {
  flex: 1;
  min-width: 12rem;
  overflow-wrap: anywhere;
}

.bielu-signalr__state[data-connected='true'] {
  color: #2e7d32;
}

.bielu-signalr__events {
  margin: 0;
  padding-left: 1.1rem;
}

.bielu-signalr__log {
  list-style: none;
  margin: 0;
  padding: 0.5rem;
  max-height: 16rem;
  overflow: auto;
  border: 1px solid var(--scalar-border-color, #ddd);
  border-radius: 6px;
  font-family: var(--scalar-font-code, monospace);
  font-size: 0.8rem;
}

.bielu-signalr__log li {
  display: flex;
  gap: 0.5rem;
  padding: 0.1rem 0;
}

.bielu-signalr__time {
  opacity: 0.6;
}

.bielu-signalr__badge {
  text-transform: uppercase;
  font-size: 0.65rem;
  align-self: center;
  border-radius: 3px;
  padding: 0 0.25rem;
  background: var(--scalar-background-2, #eee);
}

.bielu-signalr__log-in .bielu-signalr__badge {
  background: #e3f2fd;
}

.bielu-signalr__log-out .bielu-signalr__badge {
  background: #fff3e0;
}

.bielu-signalr__muted {
  opacity: 0.65;
}
</style>
