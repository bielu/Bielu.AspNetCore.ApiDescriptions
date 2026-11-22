// @vitest-environment happy-dom
import { mount, type VueWrapper } from '@vue/test-utils'
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'
import ArazzoViewSwitch, { viewTabId } from '../src/components/ArazzoViewSwitch.vue'
import ArazzoWorkflow from '../src/components/ArazzoWorkflow.vue'
import { resetWorkflowView } from '../src/helpers/view-mode'
import type { ArazzoDocument, ArazzoWorkflow as Workflow } from '../src/types'

const views = [
  { value: 'technical' as const, label: 'Technical' },
  { value: 'data' as const, label: 'Data flow' },
  { value: 'sequence' as const, label: 'Sequence' },
  { value: 'pages' as const, label: 'Pages' },
]

const listOrders: Workflow = {
  workflowId: 'listOrders',
  steps: [{ stepId: 'load', operationId: 'listOrders', 'x-page': 'orders', 'x-label': 'Load the order list' } as never],
}

const shop = {
  arazzo: '1.1.0',
  'x-shop-pages': { orders: { title: 'Order list', route: '/orders' } },
  workflows: [listOrders],
} as ArazzoDocument

const mounted: VueWrapper[] = []

const mountWorkflow = () => {
  const wrapper = mount(ArazzoWorkflow, {
    attachTo: globalThis.document.body,
    props: {
      view: { id: listOrders.workflowId, workflow: listOrders, steps: listOrders.steps!.map((step) => ({ id: `listOrders/${step.stepId}`, step })) },
      document: shop,
      documents: {},
      arazzoOptions: { pagesExtension: 'x-shop-pages', pageExtension: 'x-page', labelExtension: 'x-label' },
    },
  })
  mounted.push(wrapper)
  return wrapper
}

beforeEach(() => {
  localStorage.clear()
  resetWorkflowView()
})

afterEach(() => {
  mounted.splice(0).forEach((wrapper) => wrapper.unmount())
  localStorage.clear()
  resetWorkflowView()
})

describe('ArazzoViewSwitch', () => {
  it('offers each view as a tab of a tab list, selected and focusable for the one shown', () => {
    // Act
    const wrapper = mount(ArazzoViewSwitch, { props: { views, panelId: 'panel', view: 'data' } })

    // Assert
    const tabs = wrapper.findAll('button')
    expect(wrapper.attributes()).toMatchObject({ role: 'tablist', 'aria-label': 'Overview view', class: 'ba-view-tabs' })
    expect(tabs.map((tab) => tab.text())).toEqual(['Technical', 'Data flow', 'Sequence', 'Pages'])
    expect(tabs.map((tab) => tab.attributes('role'))).toEqual(['tab', 'tab', 'tab', 'tab'])
    expect(tabs.map((tab) => tab.attributes('aria-selected'))).toEqual(['false', 'true', 'false', 'false'])
    expect(tabs.map((tab) => tab.attributes('tabindex'))).toEqual(['-1', '0', '-1', '-1'])
    expect(tabs.every((tab) => tab.attributes('type') === 'button')).toBe(true)
  })

  it('ties every tab to the panel it controls', () => {
    // Act
    const wrapper = mount(ArazzoViewSwitch, { props: { views, panelId: 'panel', view: 'data' } })

    // Assert
    const tabs = wrapper.findAll('button')
    expect(tabs.map((tab) => tab.attributes('id'))).toEqual(views.map((option) => viewTabId('panel', option.value)))
    expect(tabs.every((tab) => tab.attributes('aria-controls') === 'panel')).toBe(true)
  })

  it.each([
    ['ArrowRight', 1, 'sequence'],
    ['ArrowLeft', 1, 'technical'],
    ['ArrowRight', 3, 'technical'],
    ['ArrowLeft', 0, 'pages'],
    ['Home', 2, 'technical'],
    ['End', 0, 'pages'],
  ])('moves focus with %s from tab %i and shows %s', async (key, from, expected) => {
    // Arrange
    const onSelect = vi.fn()
    const wrapper = mount(ArazzoViewSwitch, {
      attachTo: globalThis.document.body,
      props: { views, panelId: 'panel', view: views[from]!.value, onSelect },
    })
    mounted.push(wrapper)

    // Act
    await wrapper.findAll('button')[from]!.trigger('keydown', { key })

    // Assert
    expect(onSelect.mock.calls).toEqual([[expected]])
    expect(globalThis.document.activeElement?.id).toBe(viewTabId('panel', expected))
  })

  it('leaves other keys to the browser', async () => {
    // Arrange
    const onSelect = vi.fn()
    const wrapper = mount(ArazzoViewSwitch, { props: { views, panelId: 'panel', view: 'data', onSelect } })

    // Act
    await wrapper.findAll('button')[1]!.trigger('keydown', { key: 'ArrowDown' })

    // Assert
    expect(onSelect).not.toHaveBeenCalled()
  })

  it('says which view was chosen', async () => {
    // Arrange
    const onSelect = vi.fn()
    const wrapper = mount(ArazzoViewSwitch, { props: { views, panelId: 'panel', view: 'technical', onSelect } })

    // Act
    await wrapper.findAll('button')[2]!.trigger('click')

    // Assert
    expect(onSelect.mock.calls).toEqual([['sequence']])
  })

  it('shares the width equally only when asked to fill it', () => {
    // Act
    const filled = mount(ArazzoViewSwitch, { props: { views, panelId: 'panel', view: 'data', fill: true } })
    const plain = mount(ArazzoViewSwitch, { props: { views, panelId: 'panel', view: 'data' } })

    // Assert
    expect(filled.classes()).toContain('ba-view-tabs-fill')
    expect(plain.classes()).not.toContain('ba-view-tabs-fill')
  })
})

describe('the overview view tabs', () => {
  it('sit on their own row under the title and actions, filling the overview', () => {
    // Act
    const wrapper = mountWorkflow()

    // Assert: the header holds the title and the actions only; the tabs follow it.
    const overview = wrapper.find('.ba-overview')
    expect(overview.find('.ba-overview-header .ba-view-tabs').exists()).toBe(false)
    const tabs = overview.find('.ba-overview-header + .ba-view-tabs')
    expect(tabs.exists()).toBe(true)
    expect(tabs.classes()).toContain('ba-view-tabs-fill')
    expect(tabs.findAll('button').map((tab) => tab.text())).toEqual(['Technical', 'Data flow', 'Sequence', 'Pages'])
    // The tabs control the panel after them, labelled by the shown view's tab.
    const panel = overview.find('[role="tabpanel"]')
    expect(tabs.find('button').attributes('aria-controls')).toBe(panel.attributes('id'))
    expect(panel.attributes('aria-labelledby')).toBe(tabs.find('[aria-selected="true"]').attributes('id'))
    expect(panel.find('.ba-diagram').exists()).toBe(true)
    // The title first, the expand action last.
    expect(overview.find('.ba-overview-header > :first-child').classes()).toContain('ba-overview-title')
    expect(overview.find('.ba-overview-header > :last-child').attributes('aria-label')).toBe('Expand workflow diagram')
  })

  it('make a tab bar with the legend in the expanded overview, under its title and actions', async () => {
    // Arrange: the Pages view, whose legend explains the page bands.
    const wrapper = mountWorkflow()

    // Act
    await wrapper.findAll('.ba-dialog-tab-bar button').find((tab) => tab.text() === 'Pages')!.trigger('click')

    // Assert
    const dialog = wrapper.find('dialog.ba-dialog')
    expect(dialog.find('.ba-dialog-header .ba-view-tabs').exists()).toBe(false)
    expect(dialog.find('.ba-dialog-header .ba-diagram-legend').exists()).toBe(false)
    const bar = dialog.find('.ba-dialog-header + .ba-dialog-tab-bar')
    expect(bar.find('.ba-view-tabs').classes()).not.toContain('ba-view-tabs-fill')
    expect(bar.find('.ba-view-tabs + .ba-diagram-legend').exists()).toBe(true)
    const panel = dialog.find('.ba-dialog-tab-bar + [role="tabpanel"]')
    expect(panel.attributes('aria-labelledby')).toBe(bar.find('[aria-selected="true"]').attributes('id'))
    // Its own panel, not the one beside the steps.
    expect(panel.attributes('id')).not.toBe(wrapper.find('.ba-overview [role="tabpanel"]').attributes('id'))
  })

  it('switch the view in both places at once', async () => {
    // Arrange
    const wrapper = mountWorkflow()
    const dialogTab = (name: string) => wrapper.findAll('.ba-dialog-tab-bar button').find((tab) => tab.text() === name)!

    // Act
    await dialogTab('Pages').trigger('click')

    // Assert
    expect(dialogTab('Pages').attributes('aria-selected')).toBe('true')
    expect(wrapper.findAll('.ba-overview .ba-view-tabs button').find((tab) => tab.text() === 'Pages')!.attributes('aria-selected')).toBe('true')
  })
})
