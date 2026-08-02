<script setup lang="ts">
import { computed, onBeforeUnmount, onMounted, reactive, ref, watch } from 'vue'
import { getAuthState, resolveBrokerAuth } from '../auth'
import type { BrokerDocumentModel } from '../broker-bindings'
import { loadBrokerDocuments } from '../broker-bindings'
import { loadConnections, publish, tail } from '../broker-client'
import { resolveDocuments } from '../discovery'
import type { BrokerChannelModel, BrokerConnection, BrokerDocumentRef, BrokerTailMessage } from '../types'

// `options` is the Scalar configuration Scalar binds to the element; `documents` is an optional
// explicit override. Documents are otherwise auto-discovered from `options.sources`.
const props = defineProps<{ options?: Record<string, any>; documents?: BrokerDocumentRef[] }>()

type LogEntry = { time: string; dir: 'in' | 'out' | 'sys'; text: string }

const loading = ref(true)
const error = ref<string | null>(null)
const models = ref<BrokerDocumentModel[]>([])
const connections = ref<BrokerConnection[]>([])
const selectedDocumentName = ref<string | null>(null)
const selectedConnection = ref<string | null>(null)
const selectedChannelId = ref<string | null>(null)
const publishing = ref(false)
const tailing = ref(false)
const log = reactive<LogEntry[]>([])

// Per-channel editor state, so switching channels and back does not lose what was typed.
const payloads = reactive<Record<string, string>>({})
const keys = reactive<Record<string, string>>({})

// The proxy is served by the app hosting Scalar, so same-origin is the right default. An override
// exists for the case where Scalar renders a document belonging to a different app.
const baseUrlOverride = ref('')

let tailController: AbortController | null = null

const documentNames = computed(() => models.value.map((model) => model.name))

const activeModel = computed(() =>
  models.value.find((model) => model.name === selectedDocumentName.value) ?? models.value[0] ?? null,
)

const channels = computed<BrokerChannelModel[]>(() => activeModel.value?.channels ?? [])

const selectedChannel = computed<BrokerChannelModel | null>(
  () => channels.value.find((channel) => channel.id === selectedChannelId.value) ?? channels.value[0] ?? null,
)

/** Connections whose protocol matches the selected channel's bindings. */
const usableConnections = computed(() => {
  const protocol = selectedChannel.value?.protocol
  if (!protocol) {
    return connections.value
  }
  const matching = connections.value.filter((connection) => connection.protocol === protocol)
  return matching.length > 0 ? matching : connections.value
})

const channelKey = (channel: BrokerChannelModel) => `${channel.documentName}:${channel.id}`

function currentAuth() {
  return resolveBrokerAuth(activeModel.value?.name ?? '', activeModel.value?.securitySchemes, getAuthState())
}

function append(dir: LogEntry['dir'], text: string): void {
  log.unshift({ time: new Date().toLocaleTimeString(), dir, text })
  // A busy topic would grow this without bound; the console is a tail, not an archive.
  if (log.length > 200) {
    log.splice(200)
  }
}

async function refreshConnections(): Promise<void> {
  try {
    connections.value = await loadConnections(baseUrlOverride.value, currentAuth())
    if (!selectedConnection.value && connections.value.length > 0) {
      selectedConnection.value = connections.value[0].name
    }
  } catch (cause) {
    error.value = cause instanceof Error ? cause.message : String(cause)
  }
}

async function onPublish(): Promise<void> {
  const channel = selectedChannel.value
  const connection = selectedConnection.value
  if (!channel || !connection) {
    return
  }
  publishing.value = true
  try {
    const key = channelKey(channel)
    const receipt = await publish(
      baseUrlOverride.value,
      currentAuth(),
      connection,
      channel.address,
      payloads[key] ?? '',
      keys[key] || undefined,
    )
    const position = receipt.partition != null ? ` (partition ${receipt.partition}, offset ${receipt.offset})` : ''
    append('out', `Published to ${receipt.channel}${position}`)
  } catch (cause) {
    append('sys', cause instanceof Error ? cause.message : String(cause))
  } finally {
    publishing.value = false
  }
}

function stopTail(): void {
  tailController?.abort()
  tailController = null
  tailing.value = false
}

async function startTail(): Promise<void> {
  const channel = selectedChannel.value
  const connection = selectedConnection.value
  if (!channel || !connection) {
    return
  }
  stopTail()
  const controller = new AbortController()
  tailController = controller
  tailing.value = true
  append('sys', `Tailing ${channel.address} on ${connection}`)
  try {
    for await (const message of tail(baseUrlOverride.value, currentAuth(), connection, channel.address, controller.signal)) {
      appendMessage(message)
    }
  } catch (cause) {
    if (!controller.signal.aborted) {
      append('sys', cause instanceof Error ? cause.message : String(cause))
    }
  } finally {
    if (tailController === controller) {
      tailing.value = false
      tailController = null
    }
  }
}

function appendMessage(message: BrokerTailMessage): void {
  const label = message.key ? `[${message.key}] ` : ''
  append('in', `${label}${message.payload}`)
}

watch(selectedChannel, (channel) => {
  if (!channel) {
    return
  }
  const key = channelKey(channel)
  // Seed the editor from the payload schema the first time a channel is opened.
  if (payloads[key] === undefined) {
    payloads[key] = channel.example ?? ''
  }
})

onMounted(async () => {
  try {
    const refs = resolveDocuments(props.options, props.documents)
    models.value = await loadBrokerDocuments(refs)
    selectedDocumentName.value = models.value[0]?.name ?? null
    selectedChannelId.value = models.value[0]?.channels[0]?.id ?? null
    await refreshConnections()
  } catch (cause) {
    error.value = cause instanceof Error ? cause.message : String(cause)
  } finally {
    loading.value = false
  }
})

// A tail holds an open HTTP response; leaving it running after the console unmounts would leak both
// the request and the broker consumer behind it.
onBeforeUnmount(stopTail)
</script>

<template>
  <section class="bielu-broker">
    <p v-if="loading" class="bielu-broker__status">Loading broker channels…</p>
    <p v-else-if="error" class="bielu-broker__status bielu-broker__status--error">{{ error }}</p>
    <p v-else-if="channels.length === 0" class="bielu-broker__status">
      No channels with <code>kafka</code>, <code>mqtt</code> or <code>amqp</code> bindings were found in the
      AsyncAPI document(s).
    </p>

    <template v-else>
      <header class="bielu-broker__bar">
        <label v-if="documentNames.length > 1">
          Document
          <select v-model="selectedDocumentName">
            <option v-for="name in documentNames" :key="name" :value="name">{{ name }}</option>
          </select>
        </label>

        <label>
          Channel
          <select v-model="selectedChannelId">
            <option v-for="channel in channels" :key="channel.id" :value="channel.id">
              {{ channel.address }} ({{ channel.protocol }})
            </option>
          </select>
        </label>

        <label>
          Connection
          <select v-model="selectedConnection">
            <option v-for="connection in usableConnections" :key="connection.name" :value="connection.name">
              {{ connection.name }} — {{ connection.endpoint }}
            </option>
          </select>
        </label>

        <button type="button" @click="refreshConnections">Refresh</button>
      </header>

      <p v-if="connections.length === 0" class="bielu-broker__status bielu-broker__status--error">
        The server reported no broker connections. Register one with
        <code>AddScalarBrokerBridge(o =&gt; o.AddKafkaConnection(…))</code>.
      </p>

      <p v-if="selectedChannel?.description" class="bielu-broker__description">
        {{ selectedChannel.description }}
      </p>

      <div v-if="selectedChannel" class="bielu-broker__publish">
        <label>
          Key
          <input v-model="keys[channelKey(selectedChannel)]" type="text" placeholder="(optional)" />
        </label>
        <label>
          Payload
          <textarea v-model="payloads[channelKey(selectedChannel)]" rows="8" spellcheck="false"></textarea>
        </label>
        <div class="bielu-broker__actions">
          <button type="button" :disabled="publishing || !selectedConnection" @click="onPublish">
            {{ publishing ? 'Publishing…' : 'Publish' }}
          </button>
          <button v-if="!tailing" type="button" :disabled="!selectedConnection" @click="startTail">Tail</button>
          <button v-else type="button" @click="stopTail">Stop tail</button>
        </div>
      </div>

      <ol class="bielu-broker__log">
        <li v-for="(entry, index) in log" :key="index" :class="`bielu-broker__log-${entry.dir}`">
          <span class="bielu-broker__time">{{ entry.time }}</span>
          <pre>{{ entry.text }}</pre>
        </li>
      </ol>
    </template>
  </section>
</template>

<style scoped>
.bielu-broker {
  display: flex;
  flex-direction: column;
  gap: 0.75rem;
  font-size: 0.875rem;
}

.bielu-broker__bar {
  display: flex;
  flex-wrap: wrap;
  gap: 0.75rem;
  align-items: end;
}

.bielu-broker__bar label,
.bielu-broker__publish label {
  display: flex;
  flex-direction: column;
  gap: 0.25rem;
}

.bielu-broker__publish {
  display: flex;
  flex-direction: column;
  gap: 0.5rem;
}

.bielu-broker__publish textarea {
  font-family: monospace;
  width: 100%;
}

.bielu-broker__actions {
  display: flex;
  gap: 0.5rem;
}

.bielu-broker__status--error {
  color: #b3261e;
}

.bielu-broker__description {
  margin: 0;
  opacity: 0.8;
}

.bielu-broker__log {
  list-style: none;
  margin: 0;
  padding: 0;
  display: flex;
  flex-direction: column;
  gap: 0.25rem;
  max-height: 20rem;
  overflow-y: auto;
}

.bielu-broker__log li {
  display: grid;
  grid-template-columns: 6rem 1fr;
  gap: 0.5rem;
  border-left: 3px solid transparent;
  padding-left: 0.5rem;
}

.bielu-broker__log li pre {
  margin: 0;
  white-space: pre-wrap;
  word-break: break-word;
}

.bielu-broker__log-in {
  border-left-color: #2e7d32;
}

.bielu-broker__log-out {
  border-left-color: #1565c0;
}

.bielu-broker__log-sys {
  border-left-color: #b3261e;
}

.bielu-broker__time {
  opacity: 0.6;
  font-variant-numeric: tabular-nums;
}
</style>
