// @vitest-environment happy-dom
import { mount, type VueWrapper } from '@vue/test-utils'
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

const dialogOf = (workflow: VueWrapper) => workflow.find('dialog').element as HTMLDialogElement

/** The `+`/`−` button of the `place` node, in the inline diagram or the full-screen one. */
const placeToggle = (workflow: VueWrapper, inDialog: boolean) =>
  workflow.find(`${inDialog ? 'dialog' : '.ba-overview'} [data-node-id="place"] .ba-diagram-toggle`)

const transformOf = (workflow: VueWrapper, inDialog: boolean) =>
  (workflow.find(`${inDialog ? 'dialog' : '.ba-overview'} .ba-diagram-canvas`).element as HTMLElement).style.transform

const zoomIn = async (workflow: VueWrapper, inDialog: boolean) =>
  workflow.find(`${inDialog ? 'dialog' : '.ba-overview'} .ba-diagram`).trigger('wheel', { deltaY: -100, clientX: 10, clientY: 10 })

describe('the full-screen workflow overview', () => {
  it('stays open, zoomed, when a workflow node is expanded and collapsed again', async () => {
    // Arrange
    const workflow = mountWorkflow()
    await workflow.find('button[aria-label="Expand workflow diagram"]').trigger('click')
    expect(dialogOf(workflow).open).toBe(true)
    await zoomIn(workflow, true)
    const zoomed = transformOf(workflow, true)

    // Act
    await placeToggle(workflow, true).trigger('click')
    const afterExpand = { open: dialogOf(workflow).open, groups: workflow.findAll('dialog .ba-diagram-group').length }
    await placeToggle(workflow, true).trigger('click')

    // Assert
    expect(zoomed).toContain('scale(1.1)')
    expect(afterExpand).toEqual({ open: true, groups: 1 })
    expect(dialogOf(workflow).open).toBe(true)
    expect(workflow.findAll('dialog .ba-diagram-group')).toHaveLength(0)
    expect(transformOf(workflow, true)).toBe(zoomed)
  })

  it('still closes when a step is opened from it', async () => {
    // Arrange
    const workflow = mountWorkflow()
    await workflow.find('button[aria-label="Expand workflow diagram"]').trigger('click')

    // Act
    await workflow.find('dialog [data-node-id="status"]').trigger('click')

    // Assert
    expect(dialogOf(workflow).open).toBe(false)
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
