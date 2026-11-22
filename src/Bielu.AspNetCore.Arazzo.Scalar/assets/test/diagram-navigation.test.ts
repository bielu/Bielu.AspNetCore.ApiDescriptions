// @vitest-environment happy-dom
import { DOMWrapper, flushPromises, mount, type VueWrapper } from '@vue/test-utils'
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

/** Opens a workflow's full-window overview; ScalarModal renders it in a portal at the end of <body>. */
const expand = async (view: VueWrapper, workflowSelector = '') => {
  await view.find(`${workflowSelector} button[aria-label="Expand workflow diagram"]`.trim()).trigger('click')
  await flushPromises()
}

/** Presses a key on an element of the open full-window overview. */
const pressInDialog = async (selector: string, key = 'Enter') => {
  await new DOMWrapper(globalThis.document.querySelector(`[role="dialog"] ${selector}`)).trigger('keydown', { key })
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
    await expand(view)
    await pressInDialog('[data-node-id="place"] .ba-diagram-toggle')

    // Act
    await pressInDialog('[data-node-id="place/pay"]')

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

describe('opening a step of a closed workflow in the classic layout', () => {
  const mountClassic = async () => {
    wrapper = mount(ArazzoDocument, {
      attachTo: globalThis.document.body,
      props: {
        document,
        documentName: 'd',
        documents: {},
        navigation: traverseArazzoDocument({ documentId: 'd', document: document as unknown as Record<string, unknown> }),
        options: { layout: 'classic' },
      },
    })
    await flushPromises()
    // Close checkout's section, which hides its steps.
    const close = wrapper.findAll('[id="d/workflow/checkout"] .ba-workflow-header button').find((button) => button.text() === 'Collapse')!
    await close.trigger('click')
    await flushPromises()
    return wrapper
  }

  const checkoutBody = (view: VueWrapper) => view.find('[id="d/workflow/checkout"] .ba-workflow-content').element as HTMLElement

  it('opens the workflow, then focuses the step, from the inline overview', async () => {
    // Arrange
    const view = await mountClassic()
    expect(checkoutBody(view).style.display).toBe('none')
    await press(view, '[id="d/workflow/orders"] .ba-overview [data-node-id="place"] .ba-diagram-toggle')

    // Act
    await press(view, '[id="d/workflow/orders"] .ba-overview [data-node-id="place/pay"]')

    // Assert
    expect(checkoutBody(view).style.display).not.toBe('none')
    const focused = globalThis.document.activeElement as HTMLElement
    expect(focused.closest('section.ba-step')!.id).toBe(sectionId('checkout', 'pay'))
  })

  it('does the same from the full-window overview', async () => {
    // Arrange
    const view = await mountClassic()
    expect(checkoutBody(view).style.display).toBe('none')
    await expand(view, '[id="d/workflow/orders"]')
    await pressInDialog('[data-node-id="place"] .ba-diagram-toggle')

    // Act
    await pressInDialog('[data-node-id="place/pay"]')

    // Assert
    expect(checkoutBody(view).style.display).not.toBe('none')
    expect((globalThis.document.activeElement as HTMLElement).closest('section.ba-step')!.id).toBe(sectionId('checkout', 'pay'))
  })
})

describe('steps of another Arazzo document in the overview', () => {
  it('are not links, even when this document has a step with the same id', async () => {
    // Arrange: orders calls identity's signIn; orders' own signIn also has an `authorize` step.
    const orders = {
      arazzo: '1.1.0',
      info: { title: 'Orders', version: '1.0.0' },
      'x-scalar-original-source-url': 'https://shop.test/orders.arazzo.json',
      sourceDescriptions: [{ name: 'identity', url: '/identity.arazzo.json', type: 'arazzo' }],
      workflows: [
        { workflowId: 'placeOrder', steps: [{ stepId: 'login', workflowId: '$sourceDescriptions.identity.signIn' }] },
        { workflowId: 'signIn', steps: [{ stepId: 'authorize', operationId: 'legacy' }] },
      ],
    } as unknown as Document
    const identity = {
      arazzo: '1.1.0',
      'x-scalar-original-source-url': 'https://shop.test/identity.arazzo.json',
      workflows: [{ workflowId: 'signIn', steps: [{ stepId: 'authorize', operationId: 'authorize' }] }],
    }
    wrapper = mount(ArazzoDocument, {
      attachTo: globalThis.document.body,
      props: {
        document: orders,
        documentName: 'orders',
        documents: { orders, identity },
        navigation: traverseArazzoDocument({ documentId: 'orders', document: orders as unknown as Record<string, unknown> }),
      },
    })
    await flushPromises()

    // Act
    await press(wrapper, '.ba-overview [data-node-id="login"] .ba-diagram-toggle')

    // Assert
    const nested = wrapper.find('.ba-overview [data-node-id="login/authorize"]')
    expect(nested.exists()).toBe(true)
    expect(nested.attributes('role')).toBeUndefined()
    expect(wrapper.find('.ba-overview [data-node-id="login"]').attributes('role')).toBe('link')
  })
})
