// @vitest-environment happy-dom
import { mount, type VueWrapper } from '@vue/test-utils'
import { afterEach, beforeEach, describe, expect, it } from 'vitest'
import ArazzoWorkflow from '../src/components/ArazzoWorkflow.vue'
import { resetWorkflowView, VIEW_STORAGE_KEY } from '../src/helpers/view-mode'
import type { ArazzoDocument, ArazzoWorkflow as Workflow } from '../src/types'

const listOrders: Workflow = {
  workflowId: 'listOrders',
  steps: [{ stepId: 'load', operationId: 'listOrders', 'x-page': 'orders', 'x-label': 'Load the order list' } as never],
}

const shop = {
  arazzo: '1.1.0',
  'x-shop-pages': { orders: { title: 'Order list', route: '/orders' } },
  workflows: [listOrders],
} as ArazzoDocument

let options = {}
const mounted: VueWrapper[] = []

const mountWorkflow = (document: ArazzoDocument = shop) => {
  const workflow = document.workflows![0]!
  const wrapper = mount(ArazzoWorkflow, {
    attachTo: globalThis.document.body,
    props: {
      view: { id: workflow.workflowId, workflow, steps: workflow.steps!.map((step) => ({ id: `${workflow.workflowId}/${step.stepId}`, step })) },
      document,
      documents: {},
      arazzoOptions: options,
    },
  })
  mounted.push(wrapper)
  return wrapper
}

const toggle = (wrapper: VueWrapper, name: 'Technical' | 'Pages') =>
  wrapper.findAll('.ba-overview .ba-view-toggle button').find((button) => button.text() === name)

beforeEach(() => {
  localStorage.clear()
  resetWorkflowView()
  options = { pagesExtension: 'x-shop-pages', pageExtension: 'x-page', labelExtension: 'x-label' }
})

afterEach(() => {
  mounted.splice(0).forEach((wrapper) => wrapper.unmount())
  options = {}
})

describe('the Technical | Pages toggle', () => {
  it('is hidden when the pages options are not configured', () => {
    // Arrange
    options = {}

    // Act
    const wrapper = mountWorkflow()

    // Assert
    expect(wrapper.find('.ba-view-toggle').exists()).toBe(false)
  })

  it('is hidden when the document does not use the pages extensions', () => {
    // Act
    const wrapper = mountWorkflow({ arazzo: '1.1.0', workflows: [{ workflowId: 'plain', steps: [{ stepId: 'a' }] }] })

    // Assert
    expect(wrapper.find('.ba-view-toggle').exists()).toBe(false)
  })

  it('starts on Technical and switches the overview to pages and plain labels', async () => {
    // Arrange
    const wrapper = mountWorkflow()
    const technical = wrapper.find('.ba-overview [data-node-id="load"] text').text()

    // Act
    await toggle(wrapper, 'Pages')!.trigger('click')

    // Assert
    expect(technical).toBe('load')
    expect(toggle(wrapper, 'Pages')!.attributes('aria-pressed')).toBe('true')
    expect(wrapper.find('.ba-overview [data-node-id="load"] text').text()).toBe('Load the order list')
    expect(wrapper.find('.ba-overview .ba-diagram-band text').text()).toBe('Order list · /orders')
  })

  it('keeps the zoom when switching views', async () => {
    // Arrange
    const wrapper = mountWorkflow()
    await wrapper.find('.ba-overview .ba-diagram').trigger('wheel', { deltaY: -100, clientX: 10, clientY: 10 })
    const canvas = () => (wrapper.find('.ba-overview .ba-diagram-canvas').element as HTMLElement).style.transform
    const zoomed = canvas()

    // Act
    await toggle(wrapper, 'Pages')!.trigger('click')
    await toggle(wrapper, 'Technical')!.trigger('click')

    // Assert
    expect(zoomed).toContain('scale(1.1)')
    expect(canvas()).toBe(zoomed)
  })

  it('remembers the choice for the other workflows of the page, and across reloads', async () => {
    // Arrange
    const first = mountWorkflow()

    // Act
    await toggle(first, 'Pages')!.trigger('click')
    const second = mountWorkflow()
    resetWorkflowView()
    const afterReload = mountWorkflow()

    // Assert
    expect(toggle(second, 'Pages')!.attributes('aria-pressed')).toBe('true')
    expect(localStorage.getItem(VIEW_STORAGE_KEY)).toBe('pages')
    expect(toggle(afterReload, 'Pages')!.attributes('aria-pressed')).toBe('true')
  })

  it('shows each step\'s page and label in the step list in the Pages view', async () => {
    // Arrange
    const wrapper = mountWorkflow()

    // Act
    const before = wrapper.find('.ba-step .ba-step-page').exists()
    await toggle(wrapper, 'Pages')!.trigger('click')

    // Assert
    expect(before).toBe(false)
    expect(wrapper.find('.ba-step .ba-step-page').text()).toBe('Order list · /orders Load the order list')
  })
})
