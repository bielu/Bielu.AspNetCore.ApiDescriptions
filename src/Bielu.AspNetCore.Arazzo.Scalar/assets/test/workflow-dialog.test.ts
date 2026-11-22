// @vitest-environment happy-dom
import { DOMWrapper, flushPromises, mount, type VueWrapper } from '@vue/test-utils'
import { afterEach, describe, expect, it } from 'vitest'
import ArazzoWorkflow from '../src/components/ArazzoWorkflow.vue'
import type { ArazzoDocument, ArazzoWorkflow as Workflow } from '../src/types'

const placeOrder: Workflow = { workflowId: 'placeOrder', steps: [{ stepId: 'create', operationId: 'createOrder' }, { stepId: 'pay', operationId: 'pay' }] }
const trackOrder: Workflow = {
  workflowId: 'trackOrder',
  steps: [{ stepId: 'place', workflowId: 'placeOrder' }, { stepId: 'status', operationId: 'getOrder' }],
}
const document: ArazzoDocument = { arazzo: '1.1.0', workflows: [trackOrder, placeOrder] }

let wrapper: VueWrapper | undefined

const mountWorkflow = () => {
  wrapper = mount(ArazzoWorkflow, {
    attachTo: globalThis.document.body,
    props: {
      view: {
        id: 'trackOrder',
        workflow: trackOrder,
        steps: trackOrder.steps!.map((step) => ({ id: `trackOrder/${step.stepId}`, step })),
      },
      document,
      documents: {},
    },
  })
  return wrapper
}

afterEach(() => {
  wrapper?.unmount()
  wrapper = undefined
})

/** Whether the full-screen overview is open: ScalarModal renders its dialog, in a portal at the end of <body>, only then. */
const dialogOpen = () => globalThis.document.querySelector('[role="dialog"]') !== null

/** Finds an element in the inline overview, or in the full-screen one, which lives outside the workflow's element. */
const find = (workflow: VueWrapper, inDialog: boolean, selector: string) =>
  inDialog
    ? new DOMWrapper(globalThis.document.querySelector(`[role="dialog"] ${selector}`))
    : workflow.find(`.ba-overview ${selector}`)

const expand = async (workflow: VueWrapper) => {
  await workflow.find('button[aria-label="Expand workflow diagram"]').trigger('click')
  await flushPromises()
}

/** The `+`/`−` button of the `place` node, in the inline diagram or the full-screen one. */
const placeToggle = (workflow: VueWrapper, inDialog: boolean) => find(workflow, inDialog, '[data-node-id="place"] .ba-diagram-toggle')

const transformOf = (workflow: VueWrapper, inDialog: boolean) =>
  (find(workflow, inDialog, '.ba-diagram-canvas').element as HTMLElement).style.transform

const zoomIn = async (workflow: VueWrapper, inDialog: boolean) =>
  find(workflow, inDialog, '.ba-diagram').trigger('wheel', { deltaY: -100, clientX: 10, clientY: 10 })

describe('the full-screen workflow overview', () => {
  it('stays open, zoomed, when a workflow node is expanded and collapsed again', async () => {
    // Arrange
    const workflow = mountWorkflow()
    await expand(workflow)
    expect(dialogOpen()).toBe(true)
    await zoomIn(workflow, true)
    const zoomed = transformOf(workflow, true)

    // Act
    await placeToggle(workflow, true).trigger('click')
    const afterExpand = { open: dialogOpen(), groups: globalThis.document.querySelectorAll('[role="dialog"] .ba-diagram-group').length }
    await placeToggle(workflow, true).trigger('click')

    // Assert
    expect(zoomed).toContain('scale(1.1)')
    expect(afterExpand).toEqual({ open: true, groups: 1 })
    expect(dialogOpen()).toBe(true)
    expect(globalThis.document.querySelectorAll('[role="dialog"] .ba-diagram-group')).toHaveLength(0)
    expect(transformOf(workflow, true)).toBe(zoomed)
  })

  it('still closes when a step is opened from it', async () => {
    // Arrange
    const workflow = mountWorkflow()
    await expand(workflow)

    // Act
    await find(workflow, true, '[data-node-id="status"]').trigger('click')
    await flushPromises()

    // Assert
    expect(dialogOpen()).toBe(false)
  })
})

describe('the inline workflow overview', () => {
  it('keeps its zoom when a workflow node is expanded and collapsed', async () => {
    // Arrange
    const workflow = mountWorkflow()
    await zoomIn(workflow, false)
    const zoomed = transformOf(workflow, false)

    // Act
    await placeToggle(workflow, false).trigger('click')
    await placeToggle(workflow, false).trigger('click')

    // Assert
    expect(zoomed).toContain('scale(1.1)')
    expect(transformOf(workflow, false)).toBe(zoomed)
  })
})

describe('the inline overview with an expanded workflow', () => {
  it('may grow taller to fit the expanded graph, and shrinks back once nothing is expanded', async () => {
    // Arrange
    const workflow = mountWorkflow()
    const inline = () => workflow.find('.ba-overview .ba-diagram')

    // Act
    const before = inline().classes()
    await placeToggle(workflow, false).trigger('click')
    const expanded = inline().classes()
    await placeToggle(workflow, false).trigger('click')

    // Assert
    expect(before).not.toContain('ba-diagram-grown')
    expect(expanded).toContain('ba-diagram-grown')
    expect(inline().classes()).not.toContain('ba-diagram-grown')
  })

  it('shows the name of an action on its arrow\'s tooltip', async () => {
    // Arrange
    const withActions: Workflow = {
      workflowId: 'trackOrder',
      steps: [{ stepId: 'status', operationId: 'getOrder', onFailure: [{ name: 'gone', type: 'end', criteria: [{ condition: '$statusCode == 410' }] }, { name: 'giveUp', type: 'end' }] }],
    }
    wrapper = mount(ArazzoWorkflow, {
      attachTo: globalThis.document.body,
      props: { view: { id: 'trackOrder', workflow: withActions, steps: [{ id: 'trackOrder/status', step: withActions.steps![0]! }] }, document: { arazzo: '1.1.0', workflows: [withActions] }, documents: {} },
    })

    // Act
    const labels = wrapper.findAll('.ba-overview .ba-diagram-edge-label').map((label) => ({
      text: label.find('text').text(),
      title: label.find('title').text(),
    }))

    // Assert
    expect(labels).toContainEqual({ text: 'otherwise', title: 'giveUp' })
    expect(labels).toContainEqual({ text: '$statusCode == 410', title: 'gone' })
  })
})
