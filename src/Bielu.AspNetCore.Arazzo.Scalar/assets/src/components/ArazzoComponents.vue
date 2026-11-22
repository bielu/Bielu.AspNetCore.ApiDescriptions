<script setup lang="ts">
import { computed } from 'vue'
import { provideExpressionScope } from '../helpers/expression-scope'
import { componentAnchor } from '../helpers/expressions'
import { formatValue, schemaPropertyRows } from '../helpers/format'
import type {
  ArazzoComponents,
  ArazzoDocument,
  ArazzoFailureAction,
  ArazzoParameter,
  ArazzoSuccessAction,
  PluginEventBus,
} from '../types'
import ArazzoActions from './ArazzoActions.vue'
import ArazzoAnchor from './ArazzoAnchor.vue'
import ArazzoExtensions from './ArazzoExtensions.vue'
import ArazzoSchemaProperties from './ArazzoSchemaProperties.vue'
import ArazzoValue from './ArazzoValue.vue'

/**
 * The document's reusable `components`: input schemas, parameters, and success and failure actions,
 * each under the name a `$components.<kind>.<name>` reference (or a `$ref` for inputs) uses.
 */
const props = defineProps<{
  id: string
  document: ArazzoDocument
  components: ArazzoComponents
  eventBus?: PluginEventBus
  /** workflowId → the sidebar id of that workflow's section, for `$workflows` expressions in components. */
  workflowNavIds?: Record<string, string>
}>()

// Components belong to no workflow: their `$components` and `$workflows` expressions link, `$steps` ones cannot.
provideExpressionScope(() => ({
  document: props.document,
  componentsId: props.id,
  workflowNavIds: props.workflowNavIds,
}))

const entries = <T,>(record: Record<string, T> | undefined): [string, T][] =>
  Object.entries(record ?? {})

const inputs = computed(() =>
  entries(props.components.inputs).map(([name, schema]) => ({
    name,
    schema,
    rows: schemaPropertyRows(schema),
  })),
)

const parameters = computed(() =>
  entries(
    props.components.parameters as Record<string, ArazzoParameter> | undefined,
  ),
)

const successActions = computed(() =>
  entries(
    props.components.successActions as
      Record<string, ArazzoSuccessAction> | undefined,
  ),
)

const failureActions = computed(() =>
  entries(
    props.components.failureActions as
      Record<string, ArazzoFailureAction> | undefined,
  ),
)

/** How a step or workflow refers to a component: `$components.parameters.tenant`. */
const reference = (kind: string, name: string): string =>
  `$components.${kind}.${name}`
</script>

<template>
  <section
    :id="id"
    class="ba-components"
    :data-nav-id="id">
    <div class="ba-kicker">Components</div>
    <ArazzoAnchor
      :eventBus="eventBus"
      :navId="id">
      <h2 class="ba-workflow-title">Reusable components</h2>
    </ArazzoAnchor>

    <div
      v-if="inputs.length"
      class="ba-block">
      <div class="ba-block-title">Inputs</div>
      <div
        v-for="input in inputs"
        :id="componentAnchor(id, 'inputs', input.name)"
        :key="input.name"
        class="ba-component ba-anchor-target">
        <code
          class="ba-component-name"
          :title="`Referenced as { &quot;$ref&quot;: &quot;#/components/inputs/${input.name}&quot; }`">
          {{ input.name }}
        </code>
        <ArazzoSchemaProperties
          v-if="input.rows?.length"
          :rows="input.rows" />
        <pre
          v-else
          class="ba-code"><code>{{ formatValue(input.schema) }}</code></pre>
      </div>
    </div>

    <div
      v-if="parameters.length"
      class="ba-block">
      <div class="ba-block-title">Parameters</div>
      <table class="ba-table">
        <tbody>
          <tr
            v-for="[name, parameter] in parameters"
            :id="componentAnchor(id, 'parameters', name)"
            :key="name"
            class="ba-anchor-target">
            <td>
              <code :title="reference('parameters', name)">{{ name }}</code>
            </td>
            <td>
              {{ parameter.name }}
              <span
                v-if="parameter.in"
                class="ba-muted">
                {{ parameter.in }}
              </span>
            </td>
            <td><ArazzoValue :value="parameter.value" /></td>
          </tr>
        </tbody>
      </table>
    </div>

    <ArazzoActions
      v-if="successActions.length"
      :actions="successActions.map(([, action]) => action)"
      :anchorPrefix="`${id}/successActions`"
      :keys="successActions.map(([key]) => key)"
      :document="document"
      title="Success actions" />
    <ArazzoActions
      v-if="failureActions.length"
      :actions="failureActions.map(([, action]) => action)"
      :anchorPrefix="`${id}/failureActions`"
      :keys="failureActions.map(([key]) => key)"
      :document="document"
      title="Failure actions" />

    <ArazzoExtensions :of="components" />
  </section>
</template>
