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

    // Act
    await resize(2000, layout.height / 2)

    // Assert
    expect(svgHeight()).toBeLessThan(tall)
    expect(svgHeight()).toBeLessThanOrEqual(layout.height / 2 + 0.5)
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
