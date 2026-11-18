// @vitest-environment happy-dom
import { flushPromises, mount, type VueWrapper } from '@vue/test-utils'
import { afterEach, describe, expect, it } from 'vitest'
import ArazzoDocument from '../src/components/ArazzoDocument.vue'
import { traverseArazzoDocument } from '../src/helpers/navigation'
import type { ArazzoDocument as Document } from '../src/types'

/** Orders invokes checkout; refund has a step called `pay` too, so a step id alone is ambiguous. */
const document = {
  arazzo: '1.1.0',
  info: { title: 'Shop', version: '1.0.0' },
  workflows: [
    { workflowId: 'orders', steps: [{ stepId: 'place', workflowId: 'checkout' }, { stepId: 'status', operationId: 'getOrder' }] },
    { workflowId: 'checkout', steps: [{ stepId: 'cart', operationId: 'getCart' }, { stepId: 'pay', operationId: 'pay' }] },
    { workflowId: 'refund', steps: [{ stepId: 'pay', operationId: 'refund' }] },
  ],
} as unknown as Document

const sectionId = (workflow: string, step: string) => `d/workflow/${workflow}/step/${step}`

let wrapper: VueWrapper | undefined

const mountDocument = () => {
  wrapper = mount(ArazzoDocument, {
    attachTo: globalThis.document.body,
    props: {
      document,
      documentName: 'd',
      documents: {},
      navigation: traverseArazzoDocument({ documentId: 'd', document: document as unknown as Record<string, unknown> }),
    },
  })
  return wrapper
}

afterEach(() => {
  wrapper?.unmount()
  wrapper = undefined
})

const press = async (view: VueWrapper, selector: string, key = 'Enter') => {
  await view.find(selector).trigger('keydown', { key })
  await flushPromises()
}

describe('opening steps from the overview', () => {
  it('opens a step inside an expanded workflow in that workflow\'s section, not in another with the same step id', async () => {
    // Arrange
    const view = mountDocument()
    await flushPromises()
    await press(view, '.ba-overview [data-node-id="place"] .ba-diagram-toggle')

    // Act
    await press(view, '.ba-overview [data-node-id="place/pay"]')

    // Assert
    const focused = globalThis.document.activeElement as HTMLElement
    expect(focused.classList.contains('ba-step-toggle')).toBe(true)
    expect(focused.closest('section.ba-step')!.id).toBe(sectionId('checkout', 'pay'))
  })

  it('does the same from the full-window overview, closing it first', async () => {
    // Arrange
    const view = mountDocument()
    await flushPromises()
    await press(view, 'dialog [data-node-id="place"] .ba-diagram-toggle')

    // Act
    await press(view, 'dialog [data-node-id="place/pay"]')

    // Assert
    const focused = globalThis.document.activeElement as HTMLElement
    expect(focused.closest('section.ba-step')!.id).toBe(sectionId('checkout', 'pay'))
  })

  it('reveals a step the filter hides before opening it, until the query changes', async () => {
    // Arrange: "getOrder" leaves only orders' status step.
    const view = mountDocument()
    await flushPromises()
    await view.find('.ba-filter-input').setValue('getOrder')
    const place = () => view.find(`[id="${sectionId('orders', 'place')}"]`).element.closest('li') as HTMLElement
    expect(place().style.display).toBe('none')

    // Act
    await press(view, '.ba-overview [data-node-id="place"]')

    // Assert
    expect(place().style.display).not.toBe('none')
    expect((globalThis.document.activeElement as HTMLElement).closest('section.ba-step')!.id).toBe(sectionId('orders', 'place'))
    await view.find('.ba-filter-input').setValue('getOrder ')
    await flushPromises()
    expect(place().style.display).toBe('none')
  })
})
