<script setup lang="ts">
import type { JsonValue } from '@bufbuild/protobuf'
import { ScalarButton } from '@scalar/components/button'
import { ScalarTextArea } from '@scalar/components/text-area'
import { ScalarTextInput } from '@scalar/components/text-input'
import { cva } from '@scalar/use-hooks/useBindCx'
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
const error = ref<string | null>(null)
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

// Variants as cva recipes, like Scalar's components. Class names are written out in full, so Tailwind
// finds them when it scans this file for the utilities to generate.
const methodTag = cva({
  base: 'bielu-tag',
  variants: {
    kind: {
      unary: 'bg-blue/16 text-blue',
      serverStreaming: 'bg-green/16 text-green',
      clientStreaming: 'bg-orange/16 text-orange',
      bidirectionalStreaming: 'bg-orange/16 text-orange',
    },
  },
})
const logBadge = cva({
  base: 'self-center rounded px-1 text-3xs uppercase',
  variants: { dir: { in: 'bg-blue/20', out: 'bg-orange/20', sys: 'bg-b-2' } },
})
const resultBlock = cva({
  base: 'bielu-result mt-2.5',
  variants: { ok: { true: 'border-l-green', false: 'border-l-red' } },
})

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
  error.value = null
  try {
    services.value = await loadGrpcServices(resolveDocuments(props.options, props.documents))
    // Seed a default document for the picker; the active-document scope (when detected) overrides it.
    if (services.value.length > 0 && !selectedDocumentName.value) {
      selectedDocumentName.value = services.value[0].documentName
    }
  } catch (e) {
    console.error('[gRPC Console] Failed to load gRPC services:', e)
    error.value = e instanceof Error ? e.message : String(e)
  } finally {
    loading.value = false
  }
}

init().catch((e) => {
  console.error('[gRPC Console] Unhandled error in init:', e)
})
onMounted(() => {
  activeDocumentSlug.value = detectActiveDocumentSlug()
})
</script>

<template>
  <section
    ref="rootEl"
    class="bgr mt-8 max-w-(--bgr-content-max-width,none) border-t border-border px-(--bgr-content-padding,var(--scalar-content-padding,24px)) pt-10 pb-12 font-sans text-base text-c-1"
  >
    <header class="mb-5 flex items-center gap-3">
      <h2 class="m-0 text-xl">gRPC</h2>
      <span class="bielu-pill bg-b-2 text-c-2">gRPC-Web</span>
    </header>

    <p v-if="loading" class="text-c-3">Loading gRPC services…</p>
    <div v-else-if="error">
      <p>Failed to load gRPC services.</p>
      <p class="text-c-3">{{ error }}</p>
    </div>
    <p v-else-if="filteredServices.length === 0" class="text-c-3">
      No gRPC services found in {{ activeDocumentSlug ? 'this document' : 'the AsyncAPI document(s)' }}.
    </p>

    <template v-else>
      <!-- Target bar -->
      <div class="bielu-card mb-3.5">
        <div class="flex flex-wrap items-end gap-3">
          <label v-if="showDocumentPicker" class="flex flex-col gap-1">
            <span class="bielu-caption">Document</span>
            <select class="bielu-select" v-model="selectedDocumentName">
              <option v-for="doc in uniqueDocuments" :key="doc" :value="doc">{{ doc }}</option>
            </select>
          </label>
          <label class="flex flex-col gap-1">
            <span class="bielu-caption">Service</span>
            <select class="bielu-select" v-model="selectedKey">
              <option v-for="service in filteredServices" :key="serviceKey(service)" :value="serviceKey(service)">
                {{ service.service }}
              </option>
            </select>
          </label>
          <label class="flex min-w-48 flex-1 flex-col gap-1">
            <span class="bielu-caption">Base URL</span>
            <ScalarTextInput v-model="baseUrlOverride" class="px-2 py-1.5" spellcheck="false" />
          </label>
        </div>
        <div v-if="selectedService?.protoFile" class="mt-3 flex flex-wrap items-center gap-3">
          <span class="text-xxs text-c-3">{{ selectedService.protoFile }}</span>
        </div>
      </div>

      <!-- Two columns on wide screens (method invocation | message log); stacked on narrow. -->
      <div class="grid grid-cols-1 items-start gap-x-8 min-[60rem]:grid-cols-2">
      <!-- Left column: pick a method, edit the prefilled request, invoke over gRPC-Web. -->
      <div class="min-w-0">
      <h3 class="mt-5 mb-2 text-base font-medium">Methods</h3>
      <p v-if="!selectedService || selectedService.methods.length === 0" class="text-c-3">
        No RPC methods declared for this service.
      </p>
      <article v-else class="bielu-card mb-3.5">
        <div class="flex flex-wrap items-end gap-3">
          <label class="flex min-w-48 flex-1 flex-col gap-1">
            <span class="bielu-caption">Method</span>
            <select class="bielu-select" v-model="selectedMethodId">
              <option v-for="method in selectedService.methods" :key="method.id" :value="method.id">
                {{ method.method }} · {{ methodTypeLabel(method) }}
              </option>
            </select>
          </label>
          <label v-if="selectedMethod && selectedMethod.messages.length > 1" class="flex min-w-48 flex-1 flex-col gap-1">
            <span class="bielu-caption">Message</span>
            <select
              class="bielu-select"
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
          <div class="mt-3.5 flex items-center gap-2.5">
            <span :class="methodTag({ kind: selectedMethod.methodType ?? 'unary' })">
              {{ methodTypeLabel(selectedMethod) }}
            </span>
            <span class="font-code font-medium">{{ selectedMethod.method }}</span>
            <span v-if="!isInvokable(selectedMethod)" class="bielu-pill bg-orange/16 text-orange">not invokable from the browser</span>
          </div>
          <p v-if="selectedMethod.summary" class="mt-1.5 mb-0 text-c-3">{{ selectedMethod.summary }}</p>
          <p class="mt-1.5 mb-0 text-xxs text-c-3">
            {{ selectedMethod.requestType ?? '?' }} → {{ selectedMethod.responseType ?? '?'
            }}<template v-if="selectedMethod.deadlineSeconds"> · deadline {{ selectedMethod.deadlineSeconds }}s</template>
            <template v-if="selectedMethod.idempotencyLevel"> · {{ selectedMethod.idempotencyLevel }}</template>
          </p>

          <template v-if="isInvokable(selectedMethod)">
            <div class="mt-2.5 flex items-center justify-between">
              <span class="text-xxs text-c-3">Request message (JSON)</span>
              <button
                type="button"
                class="cursor-pointer border-0 bg-transparent p-0 text-xxs text-blue disabled:cursor-not-allowed disabled:opacity-50"
                :disabled="selectedMethod.messages.length === 0"
                @click="applyExample(selectedMethod, selectedMessageIndex[methodKey(selectedMethod)] ?? 0)"
              >
                ↺ Reset to example
              </button>
            </div>
            <ScalarTextArea
              v-model="requestJson[methodKey(selectedMethod)]"
              class="mt-2.5 p-2.5 font-code text-sm"
              rows="6"
              spellcheck="false"
              placeholder='Request message as JSON, e.g. { "name": "world" }'
            />
            <div class="mt-2.5 flex items-center justify-between">
              <span class="text-xxs text-c-3">Metadata (JSON object of headers, optional)</span>
            </div>
            <ScalarTextArea
              v-model="metadataJson[methodKey(selectedMethod)]"
              class="mt-2.5 p-2.5 font-code text-sm"
              rows="2"
              spellcheck="false"
              placeholder='{ "x-correlation-id": "1234" }'
            />
            <div class="mt-2.5 flex items-center justify-between">
              <span class="text-xxs text-c-3">{{ methodUrl }}</span>
              <!-- ScalarButton marks itself aria-disabled but still clicks; testRequest returns while invoking. -->
              <ScalarButton size="sm" :disabled="invoking" @click="testRequest(selectedMethod)">
                ▶ Test Request
              </ScalarButton>
            </div>
            <pre
              v-if="results[methodKey(selectedMethod)]"
              :class="resultBlock({ ok: results[methodKey(selectedMethod)].ok })"
            >{{ results[methodKey(selectedMethod)].text }}</pre>
          </template>
          <p v-else class="mt-1.5 mb-0 text-c-3">
            gRPC-Web supports unary and server-streaming calls only; client- and bidirectional-streaming
            methods are shown for documentation. The example request below is generated from the AsyncAPI
            payload schema.
          </p>
          <pre
            v-if="!isInvokable(selectedMethod) && selectedMethod.messages.length > 0"
            class="bielu-result mt-2.5 border-l-c-3"
          >{{ selectedMethod.messages[0].example }}</pre>
        </template>
      </article>
      </div>

      <!-- Right column: the live message log. -->
      <div class="min-w-0">
      <h3 class="mt-5 mb-2 text-base font-medium">Messages</h3>
      <ul class="bielu-log">
        <li v-for="(entry, index) in log" :key="index" class="flex gap-2 py-0.5">
          <span class="opacity-55">{{ entry.time }}</span>
          <span :class="logBadge({ dir: entry.dir })">{{ entry.dir }}</span>
          <span>{{ entry.text }}</span>
        </li>
        <li v-if="log.length === 0" class="text-c-3">No messages yet.</li>
      </ul>
      </div>
      </div>
    </template>
  </section>
</template>

<!-- Not scoped: the utilities are scoped to the console element instead (see ../style.css). -->
<style src="../style.css"></style>
