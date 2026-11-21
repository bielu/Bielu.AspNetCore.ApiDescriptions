<script setup lang="ts">
import { ScalarButton } from '@scalar/components/button'
import { ScalarTextArea } from '@scalar/components/text-area'
import { ScalarTextInput } from '@scalar/components/text-input'
import { cva } from '@scalar/use-hooks/useBindCx'
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
const refreshingConnections = ref(false)
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

/**
 * Connections whose protocol matches the selected channel's bindings. There is deliberately no
 * fallback to the other connections: the bridge addresses whatever channel it is handed, so offering
 * a Kafka connection for an MQTT channel would publish to a Kafka topic named after the MQTT topic.
 */
const usableConnections = computed(() => {
  const protocol = selectedChannel.value?.protocol
  if (!protocol) {
    return connections.value
  }
  return connections.value.filter((connection) => connection.protocol === protocol)
})

const channelKey = (channel: BrokerChannelModel) => `${channel.documentName}:${channel.id}`

// A cva recipe, like Scalar's components. Class names are written out in full, so Tailwind finds them
// when it scans this file for the utilities to generate.
const logEntry = cva({
  base: 'grid grid-cols-[6rem_1fr] gap-2 border-l-3 pl-2',
  variants: { dir: { in: 'border-l-green', out: 'border-l-blue', sys: 'border-l-red' } },
})

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
  // ScalarButton's `disabled` only sets aria-disabled, so a click still lands here mid-refresh.
  if (refreshingConnections.value) {
    return
  }
  refreshingConnections.value = true
  try {
    connections.value = await loadConnections(baseUrlOverride.value, currentAuth())
    if (!selectedConnection.value && connections.value.length > 0) {
      selectedConnection.value = connections.value[0].name
    }
  } catch (cause) {
    error.value = cause instanceof Error ? cause.message : String(cause)
  } finally {
    refreshingConnections.value = false
  }
}

async function onPublish(): Promise<void> {
  const channel = selectedChannel.value
  const connection = selectedConnection.value
  if (!channel || !connection || publishing.value) {
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

// The usable set narrows when the selected channel's protocol changes, and shrinks on every
// refresh; a name that has fallen out of it would otherwise still reach onPublish/startTail.
watch(usableConnections, (list) => {
  if (selectedConnection.value && !list.some((connection) => connection.name === selectedConnection.value)) {
    selectedConnection.value = list[0]?.name ?? null
  }
})

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
  <section class="bielu-broker flex flex-col gap-3 text-sm">
    <p v-if="loading">Loading broker channels…</p>
    <p v-else-if="error" class="text-red">{{ error }}</p>
    <p v-else-if="channels.length === 0">
      No channels with <code>kafka</code>, <code>mqtt</code> or <code>amqp</code> bindings were found in the
      AsyncAPI document(s).
    </p>

    <template v-else>
      <header class="flex flex-wrap items-end gap-3">
        <label v-if="documentNames.length > 1" class="flex flex-col gap-1">
          Document
          <select class="bielu-select" v-model="selectedDocumentName">
            <option v-for="name in documentNames" :key="name" :value="name">{{ name }}</option>
          </select>
        </label>

        <label class="flex flex-col gap-1">
          Channel
          <select class="bielu-select" v-model="selectedChannelId">
            <option v-for="channel in channels" :key="channel.id" :value="channel.id">
              {{ channel.address }} ({{ channel.protocol }})
            </option>
          </select>
        </label>

        <label class="flex flex-col gap-1">
          Connection
          <select class="bielu-select" v-model="selectedConnection">
            <option v-for="connection in usableConnections" :key="connection.name" :value="connection.name">
              {{ connection.name }} — {{ connection.endpoint }}
            </option>
          </select>
        </label>

        <ScalarButton size="sm" variant="outlined" :disabled="refreshingConnections" @click="refreshConnections">
          Refresh
        </ScalarButton>
      </header>

      <p v-if="connections.length === 0" class="text-red">
        The server reported no broker connections. Register one with
        <code>AddScalarBrokerBridge(o =&gt; o.AddKafkaConnection(…))</code>.
      </p>
      <p v-else-if="selectedChannel && usableConnections.length === 0" class="text-red">
        The server has no <code>{{ selectedChannel.protocol }}</code> connection for this channel. Only a
        Kafka driver ships today; an MQTT or AMQP channel needs a driver that implements
        <code>IBrokerBridge</code>.
      </p>

      <p v-if="selectedChannel?.description" class="m-0 opacity-80">
        {{ selectedChannel.description }}
      </p>

      <div v-if="selectedChannel" class="flex flex-col gap-2">
        <label class="flex flex-col gap-1">
          Key
          <ScalarTextInput v-model="keys[channelKey(selectedChannel)]" class="px-2 py-1.5 text-sm" placeholder="(optional)" />
        </label>
        <label class="flex flex-col gap-1">
          Payload
          <ScalarTextArea
            v-model="payloads[channelKey(selectedChannel)]"
            rows="8"
            spellcheck="false"
            class="p-2.5 font-code text-sm"
          />
        </label>
        <div class="flex gap-2">
          <!-- ScalarButton marks itself aria-disabled but still clicks; the handlers check the same state. -->
          <ScalarButton size="sm" :disabled="publishing || !selectedConnection" @click="onPublish">
            {{ publishing ? 'Publishing…' : 'Publish' }}
          </ScalarButton>
          <ScalarButton v-if="!tailing" size="sm" variant="outlined" :disabled="!selectedConnection" @click="startTail">
            Tail
          </ScalarButton>
          <ScalarButton v-else size="sm" variant="outlined" @click="stopTail">Stop tail</ScalarButton>
        </div>
      </div>

      <ol class="m-0 flex max-h-80 list-none flex-col gap-1 overflow-y-auto p-0">
        <li
          v-for="(entry, index) in log"
          :key="index"
          :class="logEntry({ dir: entry.dir })"
        >
          <span class="opacity-60 tabular-nums">{{ entry.time }}</span>
          <pre class="m-0 whitespace-pre-wrap wrap-break-word">{{ entry.text }}</pre>
        </li>
      </ol>
    </template>
  </section>
</template>

<!-- Not scoped: the utilities are scoped to the console element instead (see ../style.css). -->
<style src="../style.css"></style>
