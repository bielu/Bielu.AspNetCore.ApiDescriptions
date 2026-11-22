<script setup lang="ts">
import { nextTick } from 'vue'
import type { FlowLink, FlowValue } from '../helpers/data-flow-values'
import { goToAnchor } from '../helpers/navigation'
import { navigationHref, openInOtherDocument } from '../helpers/target-link'
import ArazzoSchemaProperties from './ArazzoSchemaProperties.vue'

/**
 * What one Data flow arrow carries: for each value, where it is made (the expression and the response,
 * message or inputs it reads), its schema as the loaded documents describe it, a link to where that schema
 * is defined, and where the receiving side uses it.
 */
defineProps<{
  from: string
  to: string
  values: FlowValue[]
}>()

const emit = defineEmits<{
  /** A link was followed; the dialogs over the page should close. */
  navigate: []
}>()

const hrefOf = (link: FlowLink): string =>
  link.navId
    ? navigationHref(link.navId, window.location)
    : `${window.location.pathname}${window.location.search}#${link.anchor ?? ''}`

/**
 * A schema in another document opens its page in Scalar; an input of this workflow scrolls to its row. Followed
 * from the keyboard (a click with no pointer, `detail` 0), the row also takes the focus, so the keyboard carries on
 * from where the reader was taken.
 */
const follow = (event: MouseEvent, link: FlowLink) => {
  emit('navigate')
  if (link.navId) {
    openInOtherDocument(event, hrefOf(link))
  } else if (link.anchor) {
    event.preventDefault()
    void goToAnchor(
      event.currentTarget as Element,
      { anchor: link.anchor },
      nextTick,
      { focus: event.detail === 0 },
    )
  }
}
</script>

<template>
  <div class="ba-flow-details">
    <p class="ba-muted ba-flow-route">
      From <strong>{{ from }}</strong> to <strong>{{ to }}</strong>
    </p>
    <section
      v-for="value in values"
      :key="value.name"
      class="ba-flow-value">
      <h4 class="ba-block-title">
        <code>{{ value.name }}</code>
      </h4>
      <dl class="ba-flow-facts">
        <dt>Set from</dt>
        <dd>
          <code>{{ value.source }}</code>
          <span
            v-if="value.origin"
            class="ba-muted">
            · {{ value.origin }}</span
          >
        </dd>
        <template v-if="value.uses.length">
          <dt>Used as</dt>
          <dd>{{ value.uses.join(', ') }}</dd>
        </template>
        <template v-if="value.link">
          <dt>Defined in</dt>
          <dd>
            <a
              class="ba-flow-link"
              :href="hrefOf(value.link)"
              @click="follow($event, value.link)"
              >{{ value.link.label }} ↗</a
            >
          </dd>
        </template>
      </dl>
      <ArazzoSchemaProperties
        v-if="value.schema"
        :rows="[value.schema]" />
      <p
        v-else
        class="ba-muted ba-flow-no-schema">
        No schema found: the value's document isn't loaded or doesn't describe
        it.
      </p>
    </section>
  </div>
</template>
