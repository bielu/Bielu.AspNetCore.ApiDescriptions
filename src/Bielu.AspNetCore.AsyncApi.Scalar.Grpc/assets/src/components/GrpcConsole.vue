<script setup lang="ts">
import type { JsonValue } from '@bufbuild/protobuf'
import { computed, onMounted, reactive, ref, watch } from 'vue'
import { getAuthState, resolveGrpcAuth } from '../auth'
import { resolveDocuments } from '../discovery'
import { loadGrpcServices } from '../grpc-bindings'
import { descriptorsUrlFor, invokeServerStreaming, invokeUnary, loadDescriptorRegistry } from '../grpc-client'
import type { GrpcDocumentRef, GrpcMethodModel, GrpcServiceModel } from '../types'

// `options` is the Scalar configuration Scalar binds to the element; `documents` is an optional
// explicit override. Documents are otherwise auto-discovered from `options.sources`.
const props = defineProps<{ options?: Record<string, any>; documents?: GrpcDocumentRef[] }>()

type LogEntry = { time: string; dir: 'in' | 'out' | 'sys'; text: string }

const loading = ref(true)
const services = ref<GrpcServiceModel[]>([])
// The console's own root element, used to discover which Scalar document it is rendered under.
const rootEl = ref<HTMLElement | null>(null)
// Scalar's active document slug (null on stock Scalar / standalone use, where the picker is shown).
const activeDocumentSlug = ref<string | null>(null)
const selectedDocumentName = ref<string | null>(null)
const selectedKey = ref<string | null>(null)
const baseUrlOverride = ref('')
const invoking = ref(false)
const log = reactive<LogEntry[]>([])
// Per-method state survives service switches, so it is keyed by service + method id (methods from
// different services/documents can share an `id`, which would otherwise overwrite each other).
const requestJson = reactive<Record<string, string>>({})
const metadataJson = reactive<Record<string, string>>({})
const results = reactive<Record<string, { ok: boolean; text: string }>>({})
// Which method is selected per service (keyed by serviceKey), and which message is the active example.
const selectedMethodIds = reactive<Record<string, string>>({})
const selectedMessageIndex = reactive<Record<string, number>>({})

const serviceKey = (service: GrpcServiceModel) => `${service.documentName}:${service.channelName}`
// A service-scoped key for a method. All displayed methods belong to the selected service, so the
// selected service key uniquely scopes them.
const methodKey = (method: GrpcMethodModel) => `${selectedKey.value}:${method.id}`

const uniqueDocuments = computed(() => {
  const seen = new Set<string>()
  const docs: string[] = []
  for (const service of services.value) {
    if (!seen.has(service.documentName)) {
      seen.add(service.documentName)
      docs.push(service.documentName)
    }
  }
  return docs
})

// Scope the visible services to Scalar's active document when known (so the console reflects the
// document the user is viewing), otherwise to the manually picked document. Falls back to all
// services only when neither is set.
const scopedDocumentName = computed(() => activeDocumentSlug.value ?? selectedDocumentName.value)
const filteredServices = computed(() =>
  scopedDocumentName.value
    ? services.value.filter((service) => service.documentName === scopedDocumentName.value)
    : services.value,
)

// The document picker only makes sense when the console spans several documents itself. Once it is
// scoped to Scalar's active document, the active document is authoritative, so the picker is hidden.
const showDocumentPicker = computed(() => !activeDocumentSlug.value && uniqueDocuments.value.length > 1)

const selectedService = computed(
  () => filteredServices.value.find((service) => serviceKey(service) === selectedKey.value) ?? null,
)
// The selected method id for the current service, persisted per service so switching keeps each choice.
const selectedMethodId = computed<string | null>({
  get: () => (selectedKey.value ? selectedMethodIds[selectedKey.value] ?? null : null),
  set: (value) => {
    if (selectedKey.value && value) {
      selectedMethodIds[selectedKey.value] = value
    }
  },
})
const selectedMethod = computed(
  () => selectedService.value?.methods.find((method) => method.id === selectedMethodId.value) ?? null,
)

// gRPC-Web only supports unary and server-streaming calls (missing method types are treated as
// unary). Client-/bidi-streaming methods render as documentation with a "not invokable" badge.
const isInvokable = (method: GrpcMethodModel) =>
  method.methodType === undefined || method.methodType === 'unary' || method.methodType === 'serverStreaming'

const effectiveBaseUrl = computed(() =>
  (baseUrlOverride.value || selectedService.value?.baseUrl || '').replace(/\/+$/, ''),
)

const methodUrl = computed(() => {
  const service = selectedService.value
  const method = selectedMethod.value
  if (!service || !method) {
    return ''
  }
  return `${effectiveBaseUrl.value}/${service.service}/${method.method}`
})

function addLog(dir: LogEntry['dir'], text: string) {
  log.unshift({ time: new Date().toLocaleTimeString(), dir, text })
  if (log.length > 200) {
    log.pop()
  }
}

function methodTypeLabel(method: GrpcMethodModel): string {
  switch (method.methodType) {
    case 'serverStreaming':
      return 'SERVER STREAM'
    case 'clientStreaming':
      return 'CLIENT STREAM'
    case 'bidirectionalStreaming':
      return 'BIDI STREAM'
    default:
      return 'UNARY'
  }
}

/** Parse the request editor contents into a proto3-JSON object (empty editor → empty message). */
function parseRequest(raw: string): JsonValue {
  const text = (raw ?? '').trim()
  if (!text) {
    return {}
  }
  return JSON.parse(text) as JsonValue
}

/** Parse the metadata editor contents into flat string headers. */
function parseMetadata(raw: string): Record<string, string> {
  const text = (raw ?? '').trim()
  if (!text) {
    return {}
  }
  const parsed = JSON.parse(text)
  if (!parsed || typeof parsed !== 'object' || Array.isArray(parsed)) {
    throw new Error('metadata must be a JSON object of header names to string values')
  }
  const headers: Record<string, string> = {}
  for (const [key, value] of Object.entries(parsed)) {
    headers[key] = String(value)
  }
  return headers
}

async function testRequest(method: GrpcMethodModel) {
  const service = selectedService.value
  if (!service || invoking.value) {
    return
  }
  const key = methodKey(method)

  let request: JsonValue
  try {
    request = parseRequest(requestJson[key])
  } catch (error: any) {
    results[key] = { ok: false, text: `Invalid JSON request: ${error?.message ?? error}` }
    return
  }
  let headers: Record<string, string>
  try {
    headers = parseMetadata(metadataJson[key])
  } catch (error: any) {
    results[key] = { ok: false, text: `Invalid metadata: ${error?.message ?? error}` }
    return
  }

  // Resolve Scalar's current auth state → no-op on stock Scalar or when no scheme is selected.
  // Explicit metadata entered in the editor wins over auth-derived headers of the same name.
  const authResult = resolveGrpcAuth(service.documentName, service.securitySchemes, getAuthState())
  for (const warn of authResult.warnings) {
    addLog('sys', `Auth warning: ${warn}`)
  }
  headers = { ...authResult.headers, ...headers }

  const hasSchemes = service.securitySchemes && Object.keys(service.securitySchemes).length > 0
  if (hasSchemes && Object.keys(authResult.headers).length === 0) {
    addLog(
      'sys',
      `Auth: no credentials resolved for document "${service.documentName}" — select a security ` +
        `scheme in the Authentication panel and enter your credentials.`,
    )
  }

  invoking.value = true
  addLog('out', `${service.service}/${method.method} ${JSON.stringify(request)}`)
  try {
    const registry = await loadDescriptorRegistry(descriptorsUrlFor(effectiveBaseUrl.value))
    const callOptions = {
      baseUrl: effectiveBaseUrl.value,
      headers,
      timeoutMs: method.deadlineSeconds ? method.deadlineSeconds * 1000 : undefined,
    }
    if (method.methodType === 'serverStreaming') {
      const items: JsonValue[] = []
      await invokeServerStreaming(registry, service.service, method.method, request, callOptions, (message) => {
        items.push(message)
        addLog('in', `← ${JSON.stringify(message)}`)
      })
      results[key] = {
        ok: true,
        text: `Stream completed: ${items.length} message(s).\n${JSON.stringify(items, null, 2)}`,
      }
    } else {
      const response = await invokeUnary(registry, service.service, method.method, request, callOptions)
      results[key] = { ok: true, text: JSON.stringify(response, null, 2) }
      addLog('in', `← ${JSON.stringify(response)}`)
    }
  } catch (error: any) {
    results[key] = { ok: false, text: `Error: ${error?.message ?? error}` }
    addLog('sys', `Invoke failed: ${error?.message ?? error}`)
  } finally {
    invoking.value = false
  }
}

/** The example string for a given message of a method (falls back to an empty object). */
function exampleFor(method: GrpcMethodModel | null, index: number): string {
  return method?.messages?.[index]?.example ?? '{}'
}

/** Select a message and (re)fill the request editor with its generated example. */
function applyExample(method: GrpcMethodModel, index: number) {
  const key = methodKey(method)
  selectedMessageIndex[key] = index
  requestJson[key] = exampleFor(method, index)
}

/** Prefill a method's editor with its first example, but only if it is still untouched. */
function ensurePrefilled(method: GrpcMethodModel) {
  const key = methodKey(method)
  if (requestJson[key] === undefined) {
    applyExample(method, selectedMessageIndex[key] ?? 0)
  }
}

// Keep the selected service within the visible (document-scoped) set. Fires when the document
// changes (picker or active-document detection) and when services finish loading.
watch(
  filteredServices,
  (list) => {
    if (!list.some((service) => serviceKey(service) === selectedKey.value)) {
      selectedKey.value = list[0] ? serviceKey(list[0]) : null
    }
  },
  { immediate: true },
)

watch(selectedService, (service) => {
  baseUrlOverride.value = service?.baseUrl ?? ''
})

// When the available methods change (e.g. a different service), select the first one and prefill it.
watch(
  () => selectedService.value?.methods ?? [],
  (methods) => {
    if (!methods.some((method) => method.id === selectedMethodId.value)) {
      selectedMethodId.value = methods[0]?.id ?? null
    }
    methods.forEach(ensurePrefilled)
  },
  { immediate: true },
)

// Selecting a different method prefills its editor on first visit.
watch(selectedMethod, (method) => {
  if (method) {
    ensurePrefilled(method)
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
  services.value = await loadGrpcServices(resolveDocuments(props.options, props.documents))
  // Seed a default document for the picker; the active-document scope (when detected) overrides it.
  if (services.value.length > 0 && !selectedDocumentName.value) {
    selectedDocumentName.value = services.value[0].documentName
  }
  loading.value = false
}

void init()
onMounted(() => {
  activeDocumentSlug.value = detectActiveDocumentSlug()
})
</script>

<template>
  <section ref="rootEl" class="bgr">
    <header class="bgr__head">
      <h2 class="bgr__title">gRPC</h2>
      <span class="bgr__pill">gRPC-Web</span>
    </header>

    <p v-if="loading" class="bgr__muted">Loading gRPC services…</p>
    <p v-else-if="filteredServices.length === 0" class="bgr__muted">
      No gRPC services found in {{ activeDocumentSlug ? 'this document' : 'the AsyncAPI document(s)' }}.
    </p>

    <template v-else>
      <!-- Target bar -->
      <div class="bgr__card bgr__conn">
        <div class="bgr__conn-row">
          <label v-if="showDocumentPicker" class="bgr__field">
            <span>Document</span>
            <select v-model="selectedDocumentName">
              <option v-for="doc in uniqueDocuments" :key="doc" :value="doc">{{ doc }}</option>
            </select>
          </label>
          <label class="bgr__field">
            <span>Service</span>
            <select v-model="selectedKey">
              <option v-for="service in filteredServices" :key="serviceKey(service)" :value="serviceKey(service)">
                {{ service.service }}
              </option>
            </select>
          </label>
          <label class="bgr__field bgr__field--grow">
            <span>Base URL</span>
            <input v-model="baseUrlOverride" type="text" spellcheck="false" />
          </label>
        </div>
        <div v-if="selectedService?.protoFile" class="bgr__conn-row bgr__conn-row--foot">
          <span class="bgr__hint">{{ selectedService.protoFile }}</span>
        </div>
      </div>

      <!-- Two columns on wide screens (method invocation | message log); stacked on narrow. -->
      <div class="bgr__grid">
      <!-- Left column: pick a method, edit the prefilled request, invoke over gRPC-Web. -->
      <div class="bgr__col">
      <h3 class="bgr__section">Methods</h3>
      <p v-if="!selectedService || selectedService.methods.length === 0" class="bgr__muted">
        No RPC methods declared for this service.
      </p>
      <article v-else class="bgr__card bgr__op">
        <div class="bgr__conn-row">
          <label class="bgr__field bgr__field--grow">
            <span>Method</span>
            <select v-model="selectedMethodId">
              <option v-for="method in selectedService.methods" :key="method.id" :value="method.id">
                {{ method.method }} · {{ methodTypeLabel(method) }}
              </option>
            </select>
          </label>
          <label v-if="selectedMethod && selectedMethod.messages.length > 1" class="bgr__field bgr__field--grow">
            <span>Message</span>
            <select
              :value="selectedMessageIndex[methodKey(selectedMethod)] ?? 0"
              @change="applyExample(selectedMethod, Number(($event.target as HTMLSelectElement).value))"
            >
              <option v-for="(message, index) in selectedMethod.messages" :key="message.name" :value="index">
                {{ message.title || message.name }}
              </option>
            </select>
          </label>
        </div>

        <template v-if="selectedMethod">
          <div class="bgr__op-head bgr__op-head--spaced">
            <span class="bgr__method" :data-kind="selectedMethod.methodType ?? 'unary'">
              {{ methodTypeLabel(selectedMethod) }}
            </span>
            <span class="bgr__op-target">{{ selectedMethod.method }}</span>
            <span v-if="!isInvokable(selectedMethod)" class="bgr__badge-warn">not invokable from the browser</span>
          </div>
          <p v-if="selectedMethod.summary" class="bgr__muted bgr__op-summary">{{ selectedMethod.summary }}</p>
          <p class="bgr__hint bgr__op-summary">
            {{ selectedMethod.requestType ?? '?' }} → {{ selectedMethod.responseType ?? '?'
            }}<template v-if="selectedMethod.deadlineSeconds"> · deadline {{ selectedMethod.deadlineSeconds }}s</template>
            <template v-if="selectedMethod.idempotencyLevel"> · {{ selectedMethod.idempotencyLevel }}</template>
          </p>

          <template v-if="isInvokable(selectedMethod)">
            <div class="bgr__code-head">
              <span class="bgr__hint">Request message (JSON)</span>
              <button
                type="button"
                class="bgr__link"
                :disabled="selectedMethod.messages.length === 0"
                @click="applyExample(selectedMethod, selectedMessageIndex[methodKey(selectedMethod)] ?? 0)"
              >
                ↺ Reset to example
              </button>
            </div>
            <textarea
              v-model="requestJson[methodKey(selectedMethod)]"
              class="bgr__code"
              rows="6"
              spellcheck="false"
              placeholder='Request message as JSON, e.g. { "name": "world" }'
            />
            <div class="bgr__code-head">
              <span class="bgr__hint">Metadata (JSON object of headers, optional)</span>
            </div>
            <textarea
              v-model="metadataJson[methodKey(selectedMethod)]"
              class="bgr__code"
              rows="2"
              spellcheck="false"
              placeholder='{ "x-correlation-id": "1234" }'
            />
            <div class="bgr__op-foot">
              <span class="bgr__hint">{{ methodUrl }}</span>
              <button
                type="button"
                class="bgr__btn bgr__btn--primary"
                :disabled="invoking"
                @click="testRequest(selectedMethod)"
              >
                ▶ Test Request
              </button>
            </div>
            <pre
              v-if="results[methodKey(selectedMethod)]"
              class="bgr__result"
              :data-ok="results[methodKey(selectedMethod)].ok"
            >{{ results[methodKey(selectedMethod)].text }}</pre>
          </template>
          <p v-else class="bgr__muted bgr__op-summary">
            gRPC-Web supports unary and server-streaming calls only; client- and bidirectional-streaming
            methods are shown for documentation. The example request below is generated from the AsyncAPI
            payload schema.
          </p>
          <pre
            v-if="!isInvokable(selectedMethod) && selectedMethod.messages.length > 0"
            class="bgr__result bgr__result--sample"
          >{{ selectedMethod.messages[0].example }}</pre>
        </template>
      </article>
      </div>

      <!-- Right column: the live message log. -->
      <div class="bgr__col">
      <h3 class="bgr__section">Messages</h3>
      <ul class="bgr__log">
        <li v-for="(entry, index) in log" :key="index" :class="`bgr__log-${entry.dir}`">
          <span class="bgr__time">{{ entry.time }}</span>
          <span class="bgr__badge">{{ entry.dir }}</span>
          <span>{{ entry.text }}</span>
        </li>
        <li v-if="log.length === 0" class="bgr__muted">No messages yet.</li>
      </ul>
      </div>
      </div>
    </template>
  </section>
</template>

<style scoped>
.bgr {
  font-family: var(--scalar-font, system-ui, sans-serif);
  color: var(--scalar-color-1, inherit);
  font-size: var(--scalar-font-size-3, 0.9rem);
  /* Render as its own Scalar-style section: a clear top divider, generous breathing room above,
     and horizontal insets so it lines up with the operations/models instead of sitting flush
     against the viewport edge. `--bgr-content-padding` lets hosts fine-tune the inset. */
  padding: 2.5rem var(--bgr-content-padding, var(--scalar-content-padding, 24px)) 3rem;
  margin-top: 2rem;
  border-top: 1px solid var(--scalar-border-color, #e3e3e3);
  max-width: var(--bgr-content-max-width, none);
}

.bgr__head {
  display: flex;
  align-items: center;
  gap: 0.75rem;
  margin-bottom: 1.25rem;
}

/* Two-column layout on wide screens (method | log); single column when narrow.
   The target bar stays full width above this grid. */
.bgr__grid {
  display: grid;
  grid-template-columns: 1fr;
  gap: 0 2rem;
  align-items: start;
}

.bgr__col {
  min-width: 0;
}

@media (min-width: 60rem) {
  .bgr__grid {
    grid-template-columns: minmax(0, 1fr) minmax(0, 1fr);
  }
}

.bgr__title {
  margin: 0;
  font-size: 1.25rem;
}

.bgr__pill {
  font-size: 0.7rem;
  text-transform: uppercase;
  letter-spacing: 0.03em;
  padding: 0.15rem 0.5rem;
  border-radius: 999px;
  background: var(--scalar-background-2, #eee);
  color: var(--scalar-color-2, #555);
}

.bgr__card {
  border: 1px solid var(--scalar-border-color, #e3e3e3);
  border-radius: var(--scalar-radius, 8px);
  background: var(--scalar-background-1, transparent);
  padding: 0.85rem;
  margin-bottom: 0.85rem;
}

.bgr__conn-row {
  display: flex;
  flex-wrap: wrap;
  gap: 0.75rem;
  align-items: flex-end;
}

.bgr__conn-row--foot {
  margin-top: 0.75rem;
  align-items: center;
}

.bgr__field {
  display: flex;
  flex-direction: column;
  gap: 0.25rem;
}

.bgr__field > span {
  font-size: 0.72rem;
  text-transform: uppercase;
  letter-spacing: 0.03em;
  color: var(--scalar-color-3, #777);
}

.bgr__field--grow {
  flex: 1;
  min-width: 12rem;
}

.bgr__field input,
.bgr__field select {
  padding: 0.35rem 0.5rem;
  border: 1px solid var(--scalar-border-color, #ddd);
  border-radius: var(--scalar-radius, 6px);
  background: var(--scalar-background-2, #fff);
  color: inherit;
  font: inherit;
}

.bgr__section {
  margin: 1.25rem 0 0.5rem;
  font-size: 0.95rem;
}

.bgr__op-head {
  display: flex;
  align-items: center;
  gap: 0.6rem;
}

.bgr__op-head--spaced {
  margin-top: 0.85rem;
}

.bgr__code-head {
  display: flex;
  align-items: center;
  justify-content: space-between;
  margin-top: 0.6rem;
}

.bgr__link {
  font: inherit;
  font-size: 0.72rem;
  background: none;
  border: 0;
  padding: 0;
  cursor: pointer;
  color: var(--scalar-color-blue, #2563eb);
}

.bgr__link:disabled {
  opacity: 0.5;
  cursor: not-allowed;
}

.bgr__result--sample {
  border-left-color: var(--scalar-color-3, #888);
  margin-top: 0.6rem;
}

.bgr__method {
  font-family: var(--scalar-font-code, monospace);
  font-size: 0.7rem;
  font-weight: 700;
  letter-spacing: 0.03em;
  padding: 0.15rem 0.45rem;
  border-radius: var(--scalar-radius, 5px);
  background: color-mix(in srgb, var(--scalar-color-blue, #2563eb) 16%, transparent);
  color: var(--scalar-color-blue, #2563eb);
  white-space: nowrap;
}

.bgr__method[data-kind='serverStreaming'] {
  background: color-mix(in srgb, var(--scalar-color-green, #16a34a) 16%, transparent);
  color: var(--scalar-color-green, #16a34a);
}

.bgr__method[data-kind='clientStreaming'],
.bgr__method[data-kind='bidirectionalStreaming'] {
  background: color-mix(in srgb, var(--scalar-color-orange, #d97706) 16%, transparent);
  color: var(--scalar-color-orange, #d97706);
}

.bgr__badge-warn {
  font-size: 0.68rem;
  text-transform: uppercase;
  letter-spacing: 0.03em;
  padding: 0.12rem 0.4rem;
  border-radius: 999px;
  background: color-mix(in srgb, var(--scalar-color-orange, #d97706) 16%, transparent);
  color: var(--scalar-color-orange, #d97706);
  white-space: nowrap;
}

.bgr__op-target {
  font-family: var(--scalar-font-code, monospace);
  font-weight: 600;
}

.bgr__op-summary {
  margin: 0.4rem 0 0;
}

.bgr__code {
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

.bgr__op-foot {
  display: flex;
  align-items: center;
  justify-content: space-between;
  margin-top: 0.6rem;
}

.bgr__hint {
  font-size: 0.72rem;
  color: var(--scalar-color-3, #888);
  overflow-wrap: anywhere;
}

.bgr__btn {
  font: inherit;
  font-weight: 600;
  padding: 0.4rem 0.8rem;
  border-radius: var(--scalar-radius, 6px);
  border: 1px solid var(--scalar-border-color, #ccc);
  background: var(--scalar-background-2, #f4f4f4);
  color: inherit;
  cursor: pointer;
}

.bgr__btn--primary {
  background: var(--scalar-button-1, #111);
  color: var(--scalar-button-1-color, #fff);
  border-color: transparent;
}

.bgr__btn:disabled {
  opacity: 0.5;
  cursor: not-allowed;
}

.bgr__result {
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

.bgr__result[data-ok='false'] {
  border-left-color: var(--scalar-color-red, #dc2626);
}

.bgr__log {
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

.bgr__log li {
  display: flex;
  gap: 0.5rem;
  padding: 0.12rem 0;
}

.bgr__time {
  opacity: 0.55;
}

.bgr__badge {
  text-transform: uppercase;
  font-size: 0.62rem;
  align-self: center;
  border-radius: 3px;
  padding: 0 0.25rem;
  background: var(--scalar-background-2, #eee);
}

.bgr__log-in .bgr__badge {
  background: color-mix(in srgb, var(--scalar-color-blue, #2563eb) 20%, transparent);
}

.bgr__log-out .bgr__badge {
  background: color-mix(in srgb, var(--scalar-color-orange, #d97706) 20%, transparent);
}

.bgr__muted {
  color: var(--scalar-color-3, #888);
}
</style>
