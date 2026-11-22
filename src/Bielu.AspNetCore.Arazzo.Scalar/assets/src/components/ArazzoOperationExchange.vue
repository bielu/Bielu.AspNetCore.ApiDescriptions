<script setup lang="ts">
import { Badge as ScalarBadge } from '@scalar/blocks/shared'
import { computed } from 'vue'
import { formatValue, schemaPropertyRows } from '../helpers/format'
import { renderMarkdown } from '../helpers/markdown'
import {
  operationRequestBody,
  operationResponses,
  responseHandling,
  type ExchangeContent,
} from '../helpers/openapi-exchange'
import type { StepTarget } from '../helpers/resolve-target'
import type {
  ArazzoCriterion,
  ArazzoFailureAction,
  ArazzoSuccessAction,
} from '../types'
import ArazzoSchemaProperties from './ArazzoSchemaProperties.vue'

/**
 * What the OpenAPI operation a step calls accepts and returns: its request body schema, and each
 * response by status with its schema, so the step's payload, success criteria and `$response.body`
 * outputs can be read against them. A response the step's criteria or actions check is marked.
 */
const props = defineProps<{
  target: StepTarget
  documents: Record<string, unknown>
  /** The content type the step sends, so the matching media type of the request body is the one shown. */
  contentType?: string
  successCriteria?: ArazzoCriterion[]
  actions: (ArazzoSuccessAction | ArazzoFailureAction)[]
}>()

const requestBody = computed(() =>
  operationRequestBody(props.target, props.documents),
)

/** The media type shown: the one the step sends, else the first the operation lists. */
const requestContent = computed(() => {
  const contents = requestBody.value?.contents ?? []
  return (
    contents.find((content) => content.contentType === props.contentType) ??
    contents[0]
  )
})

const otherRequestTypes = computed(() =>
  (requestBody.value?.contents ?? [])
    .filter((content) => content !== requestContent.value)
    .map((content) => content.contentType),
)

const responses = computed(() =>
  operationResponses(props.target, props.documents),
)

const handling = computed(() =>
  responseHandling(responses.value, props.successCriteria, props.actions),
)

const rowsOf = (
  content: ExchangeContent | undefined,
  root: Record<string, unknown>,
) => (content?.schema ? schemaPropertyRows(content.schema, root) : undefined)

/** `ba-status-2` for a 2xx, so success, redirect and error statuses read apart at a glance. */
const statusClass = (status: string): string =>
  `ba-status-${/^\d/.test(status) ? status[0] : 'default'}`
</script>

<template>
  <div
    v-if="requestBody"
    class="ba-target-body">
    <div class="ba-block-title">
      Operation request body
      <span
        v-if="requestContent"
        class="ba-muted">
        {{ requestContent.contentType }}
      </span>
      <span
        v-if="requestBody.required"
        class="ba-required">
        required
      </span>
    </div>
    <div
      v-if="requestBody.description"
      class="ba-markdown ba-target-summary"
      v-html="renderMarkdown(requestBody.description)" />
    <ArazzoSchemaProperties
      v-if="rowsOf(requestContent, requestBody.root)?.length"
      :rows="rowsOf(requestContent, requestBody.root)!" />
    <pre
      v-else-if="requestContent?.schema"
      class="ba-code"><code>{{ formatValue(requestContent.schema) }}</code></pre>
    <details
      v-if="requestContent?.example !== undefined"
      class="ba-extensions">
      <summary>Example request</summary>
      <pre
        class="ba-code"><code>{{ formatValue(requestContent.example) }}</code></pre>
    </details>
    <div
      v-if="otherRequestTypes.length"
      class="ba-muted">
      Also accepts {{ otherRequestTypes.join(', ') }}
    </div>
  </div>

  <div
    v-if="responses.length"
    class="ba-target-responses">
    <div class="ba-block-title">Operation responses</div>
    <ul class="ba-responses">
      <li
        v-for="response in responses"
        :key="response.status">
        <details
          v-if="response.contents.length || response.headers.length"
          class="ba-response">
          <summary>
            <code
              class="ba-status"
              :class="statusClass(response.status)"
              >{{ response.status }}</code
            >
            <span class="ba-response-description">{{
              response.description
            }}</span>
            <ScalarBadge
              v-if="handling[response.status]?.success"
              class="ba-badge ba-response-handled"
              color="var(--scalar-color-green)"
              title="The step's success criteria check for this status">
              Success criteria
            </ScalarBadge>
            <ScalarBadge
              v-for="action in handling[response.status]?.actions ?? []"
              :key="action"
              class="ba-badge"
              :title="`Action ${action} checks for this status`">
              {{ action }}
            </ScalarBadge>
          </summary>
          <template
            v-for="content in response.contents.slice(0, 1)"
            :key="content.contentType">
            <div class="ba-muted">{{ content.contentType }}</div>
            <ArazzoSchemaProperties
              v-if="rowsOf(content, response.root)?.length"
              :rows="rowsOf(content, response.root)!" />
            <pre
              v-else-if="content.schema"
              class="ba-code"><code>{{ formatValue(content.schema) }}</code></pre>
            <details
              v-if="content.example !== undefined"
              class="ba-extensions">
              <summary>Example response</summary>
              <pre
                class="ba-code"><code>{{ formatValue(content.example) }}</code></pre>
            </details>
          </template>
          <div
            v-if="response.contents.length > 1"
            class="ba-muted">
            Also
            {{
              response.contents
                .slice(1)
                .map((content) => content.contentType)
                .join(', ')
            }}
          </div>
          <div
            v-if="response.headers.length"
            class="ba-muted">
            Headers:
            <code
              v-for="(header, index) in response.headers"
              :key="header"
              >{{ index ? ', ' : '' }}{{ header }}</code
            >
          </div>
        </details>
        <div
          v-else
          class="ba-response ba-response-empty">
          <code
            class="ba-status"
            :class="statusClass(response.status)"
            >{{ response.status }}</code
          >
          <span class="ba-response-description">{{
            response.description
          }}</span>
          <ScalarBadge
            v-if="handling[response.status]?.success"
            class="ba-badge ba-response-handled"
            color="var(--scalar-color-green)"
            title="The step's success criteria check for this status">
            Success criteria
          </ScalarBadge>
          <ScalarBadge
            v-for="action in handling[response.status]?.actions ?? []"
            :key="action"
            class="ba-badge"
            :title="`Action ${action} checks for this status`">
            {{ action }}
          </ScalarBadge>
        </div>
      </li>
    </ul>
  </div>
</template>
