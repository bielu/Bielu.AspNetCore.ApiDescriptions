// @vitest-environment happy-dom
import { mount } from '@vue/test-utils'
import { describe, expect, it } from 'vitest'
import ArazzoWorkflowDiagram from '../src/components/ArazzoWorkflowDiagram.vue'
import {
  buildWorkflowGraph,
  endCopyId,
  FAILURE_NODE_ID,
  layoutWorkflowGraph,
  splitSharedEnds,
  SUCCESS_NODE_ID,
  type WorkflowGraph,
} from '../src/helpers/workflow-graph'
import type { ArazzoDocument, ArazzoWorkflow } from '../src/types'

/** Three steps that each end the workflow, on success or on a 5xx: one row, all sharing Success and Failed. */
const parallel: ArazzoWorkflow = {
  workflowId: 'everyField',
  steps: ['create', 'confirmed', 'read'].map((stepId) => ({
    stepId,
    onSuccess: [{ name: 'done', type: 'end' }],
    onFailure: [{ name: 'giveUp', type: 'end', criteria: [{ condition: '$statusCode >= 500' }] }],
  })),
}

const documentOf = (workflow: ArazzoWorkflow): ArazzoDocument => ({ arazzo: '1.1.0', workflows: [workflow] })

describe('splitSharedEnds', () => {
  it('draws an end node once per step that leads to it', () => {
    // Arrange
    const graph = buildWorkflowGraph(documentOf(parallel), parallel)

    // Act
    const split = splitSharedEnds(graph)

    // Assert
    const successes = split.nodes.filter((node) => node.kind === 'success').map((node) => node.id)
    expect(successes).toEqual([SUCCESS_NODE_ID, endCopyId(SUCCESS_NODE_ID, 1), endCopyId(SUCCESS_NODE_ID, 2)])
    for (const end of split.nodes.filter((node) => node.kind === 'success' || node.kind === 'failure')) {
      expect(new Set(split.edges.filter((edge) => edge.to === end.id).map((edge) => edge.from)).size).toBe(1)
    }
    expect(split.edges.find((edge) => edge.from === 'read' && edge.kind === 'failure')?.to).toBe(endCopyId(FAILURE_NODE_ID, 2))
  })

  it('leaves the graph it is given unchanged', () => {
    // Arrange
    const graph = buildWorkflowGraph(documentOf(parallel), parallel)
    const before = JSON.stringify(graph)

    // Act
    splitSharedEnds(graph)

    // Assert
    expect(JSON.stringify(graph)).toBe(before)
  })

  it('keeps an end node something leads on from whole', () => {
    // Arrange
    const graph: WorkflowGraph = {
      nodes: [
        { id: 'a', label: 'a', kind: 'step' },
        { id: 'b', label: 'b', kind: 'step' },
        { id: 'end', label: 'Success', kind: 'success' },
      ],
      edges: [
        { from: 'a', to: 'end', kind: 'success' },
        { from: 'b', to: 'end', kind: 'success' },
        { from: 'end', to: 'a', kind: 'return' },
      ],
      groups: [],
      bands: [],
    }

    // Act
    const split = splitSharedEnds(graph)

    // Assert
    expect(split.nodes.map((node) => node.id)).toEqual(['a', 'b', 'end'])
  })

  it('names a copy after the page its one arrow lands on, in the Pages view', () => {
    // Arrange
    const graph: WorkflowGraph = {
      nodes: [
        { id: 'a', label: 'a', kind: 'step' },
        { id: 'b', label: 'b', kind: 'step' },
        { id: 'end', label: 'Failed', kind: 'failure', actions: ['x', 'y'] },
      ],
      edges: [
        { from: 'a', to: 'end', kind: 'failure', label: 'Declined → Payment', page: 'payment', action: 'x' },
        { from: 'b', to: 'end', kind: 'failure', label: '→ Cart', page: 'cart', action: 'y' },
      ],
      groups: [],
      bands: [],
    }

    // Act
    const split = splitSharedEnds(graph)

    // Assert
    expect(split.nodes.slice(2)).toEqual([
      { id: 'end', label: 'Payment', kind: 'failure', actions: ['x'], page: 'payment' },
      { id: 'end~1', label: 'Cart', kind: 'failure', actions: ['y'], page: 'cart' },
    ])
    expect(split.edges.map((edge) => edge.label)).toEqual(['Declined', undefined])
  })
})

describe('the laid-out overview', () => {
  it('fans the arrows out to end nodes of their own, so none cross', () => {
    // Act
    const layout = layoutWorkflowGraph(buildWorkflowGraph(documentOf(parallel), parallel))

    // Assert: each step's ends sit under it, left to right in step order.
    const xOf = (id: string) => layout.nodes.find((node) => node.id === id)!.x
    const endsOf = (stepId: string) => layout.edges.filter((edge) => edge.from === stepId).map((edge) => xOf(edge.to))
    const [create, confirmed, read] = ['create', 'confirmed', 'read'].map(endsOf)
    expect(Math.max(...create!)).toBeLessThan(Math.min(...confirmed!))
    expect(Math.max(...confirmed!)).toBeLessThan(Math.min(...read!))
  })
})

describe('the Pages view', () => {
  it('puts no condition on the way on after a step succeeds', () => {
    // Arrange
    const workflow: ArazzoWorkflow = {
      workflowId: 'shop',
      steps: [
        {
          stepId: 'pay',
          'x-page': 'payment',
          successCriteria: [{ condition: '$statusCode == 201' }],
          onFailure: [{ name: 'declined', type: 'end', criteria: [{ condition: '$statusCode == 402' }] }],
        } as never,
        { stepId: 'ship', dependsOn: ['pay'], 'x-page': 'payment' } as never,
      ],
    }
    const config = { pagesExtension: 'x-pages', pageExtension: 'x-page' }

    // Act
    const technical = buildWorkflowGraph(documentOf(workflow), workflow)
    const pages = buildWorkflowGraph(documentOf(workflow), workflow, undefined, { view: 'pages', pages: config })

    // Assert
    const next = (graph: WorkflowGraph) => graph.edges.find((edge) => edge.from === 'pay' && edge.to === 'ship')
    expect(next(technical)?.label).toBe('$statusCode == 201')
    expect(next(pages)?.label).toBeUndefined()
  })

  it('draws page titles over the arrows, each on a background', () => {
    // Arrange
    const workflow: ArazzoWorkflow = { workflowId: 'shop', steps: [{ stepId: 'a', 'x-page': 'cart' } as never, { stepId: 'b' }] }
    const document = { ...documentOf(workflow), 'x-pages': { cart: { title: 'Cart', route: '/cart' } } } as ArazzoDocument
    const layout = layoutWorkflowGraph(
      buildWorkflowGraph(document, workflow, undefined, { view: 'pages', pages: { pagesExtension: 'x-pages', pageExtension: 'x-page' } }),
    )

    // Act
    const view = mount(ArazzoWorkflowDiagram, { props: { layout, stepSectionIds: {} } })

    // Assert
    const svg = view.find('svg').element
    const children = Array.from(svg.children)
    const lastEdge = children.findLastIndex((child) => child.querySelector('.ba-diagram-edge'))
    const firstTitle = children.findIndex((child) => child.classList.contains('ba-diagram-band-title'))
    expect(firstTitle).toBeGreaterThan(lastEdge)
    expect(view.findAll('.ba-diagram-band-title').map((title) => title.find('text').text())).toEqual(['Cart · /cart', 'No page'])
    expect(view.find('.ba-diagram-band-title rect').exists()).toBe(true)
    expect(view.find('.ba-diagram-band text').exists()).toBe(false)
    view.unmount()
  })
})
