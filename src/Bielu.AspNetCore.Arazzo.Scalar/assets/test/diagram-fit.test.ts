// @vitest-environment happy-dom
import { mount, type VueWrapper } from '@vue/test-utils'
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'
import { nextTick } from 'vue'
import ArazzoWorkflowDiagram from '../src/components/ArazzoWorkflowDiagram.vue'
import { buildWorkflowGraph, layoutWorkflowGraph } from '../src/helpers/workflow-graph'
import type { ArazzoWorkflow } from '../src/types'

const workflow: ArazzoWorkflow = { workflowId: 'wf', steps: [{ stepId: 'a' }, { stepId: 'b' }, { stepId: 'c' }] }
const layout = layoutWorkflowGraph(buildWorkflowGraph({ arazzo: '1.1.0', workflows: [workflow] }, workflow))

/** The size every element reports, and the ResizeObserver callback the diagram registered. */
let box = { width: 2000, height: 2000 }
let notify: (() => void) | undefined
let wrapper: VueWrapper | undefined

beforeEach(() => {
  box = { width: 2000, height: 2000 }
  vi.spyOn(HTMLElement.prototype, 'clientWidth', 'get').mockImplementation(() => box.width)
  vi.spyOn(HTMLElement.prototype, 'clientHeight', 'get').mockImplementation(() => box.height)
  vi.stubGlobal(
    'ResizeObserver',
    class {
      constructor(callback: () => void) {
        notify = callback
      }
      observe() {}
      disconnect() {}
    },
  )
})

afterEach(() => {
  wrapper?.unmount()
  wrapper = undefined
  vi.restoreAllMocks()
  vi.unstubAllGlobals()
})

const svgHeight = () => parseFloat((wrapper!.find('svg').element as SVGElement).style.height)

const resize = async (width: number, height: number) => {
  box = { width, height }
  notify?.()
  await nextTick()
}

describe('scaling the overview to its box', () => {
  it('fits a full-window overview again when the window only gets shorter', async () => {
    // Arrange
    wrapper = mount(ArazzoWorkflowDiagram, { props: { layout, stepSectionIds: {}, fit: true } })
    await nextTick()
    const tall = svgHeight()

    // Act: still room for the whole graph at a readable size.
    await resize(2000, layout.height * 0.95)

    // Assert
    expect(svgHeight()).toBeLessThan(tall)
    expect(svgHeight()).toBeLessThanOrEqual(layout.height * 0.95 + 0.5)
  })

  it('keeps a full-window overview readable, from the top, when the whole graph would be too small', async () => {
    // Arrange
    wrapper = mount(ArazzoWorkflowDiagram, { props: { layout, stepSectionIds: {}, fit: true } })
    await nextTick()

    // Act: fitting the whole graph would halve it.
    await resize(2000, layout.height / 2)

    // Assert: drawn a little larger than its natural size, top-aligned, for the reader to pan down.
    expect(svgHeight()).toBeCloseTo(layout.height * 1.25, 0)
    expect(wrapper.find('.ba-diagram-canvas').classes()).toContain('ba-diagram-canvas-top')
  })

  it('still narrows a full-window overview to a narrow window', async () => {
    // Arrange
    wrapper = mount(ArazzoWorkflowDiagram, { props: { layout, stepSectionIds: {}, fit: true } })
    await nextTick()

    // Act
    await resize(layout.width / 2, layout.height / 8)

    // Assert
    expect(svgHeight()).toBeCloseTo(layout.height / 2, 0)
  })

  it('keeps the inline overview\'s scale when only its height changes', async () => {
    // Arrange
    wrapper = mount(ArazzoWorkflowDiagram, { props: { layout, stepSectionIds: {} } })
    await nextTick()
    const before = svgHeight()

    // Act
    await resize(2000, layout.height / 2)

    // Assert
    expect(svgHeight()).toBe(before)
  })
})
