import { describe, expect, it } from 'vitest'
import { INITIAL_PAN_ZOOM, MAX_SCALE, panBy, zoomAt } from '../src/helpers/pan-zoom'
import {
  buildWorkflowGraph,
  FAILURE_NODE_ID,
  layoutWorkflowGraph,
  MAX_EDGE_LABEL_LENGTH,
  SUCCESS_NODE_ID,
} from '../src/helpers/workflow-graph'
import type { ArazzoDocument, ArazzoWorkflow } from '../src/types'

const documentWith = (workflow: ArazzoWorkflow, components?: ArazzoDocument['components']): ArazzoDocument => ({
  arazzo: '1.0.1',
  workflows: [workflow],
  components,
})

describe('buildWorkflowGraph', () => {
  it('chains steps in document order when none declares dependsOn', () => {
    // Arrange
    const workflow: ArazzoWorkflow = { workflowId: 'wf', steps: [{ stepId: 'a' }, { stepId: 'b' }, { stepId: 'c' }] }

    // Act
    const graph = buildWorkflowGraph(documentWith(workflow), workflow)

    // Assert
    expect(graph.nodes.map((node) => node.id)).toEqual(['a', 'b', 'c', SUCCESS_NODE_ID])
    expect(graph.edges).toEqual([
      { from: 'a', to: 'b', kind: 'next' },
      { from: 'b', to: 'c', kind: 'next' },
      // Arazzo ends a workflow successfully after its last step.
      { from: 'c', to: SUCCESS_NODE_ID, kind: 'success' },
    ])
  })

  it('follows dependsOn instead of document order when a step declares it', () => {
    // Arrange
    const workflow: ArazzoWorkflow = {
      workflowId: 'wf',
      steps: [{ stepId: 'a' }, { stepId: 'b' }, { stepId: 'c', dependsOn: ['a', 'missing'] }],
    }

    // Act
    const graph = buildWorkflowGraph(documentWith(workflow), workflow)

    // Assert
    expect(graph.edges).toEqual([
      { from: 'a', to: 'c', kind: 'next', label: undefined },
      { from: 'b', to: SUCCESS_NODE_ID, kind: 'success' },
      { from: 'c', to: SUCCESS_NODE_ID, kind: 'success' },
    ])
  })

  it('draws actions as labeled arrows, with end actions leading to Success/Failed', () => {
    // Arrange
    const workflow: ArazzoWorkflow = {
      workflowId: 'wf',
      steps: [
        {
          stepId: 'create',
          successCriteria: [{ condition: '$statusCode == 201' }],
          onSuccess: [{ name: 'done', type: 'end', criteria: [{ condition: '$response.body#/done == true' }] }],
          onFailure: [
            { name: 'again', type: 'retry', stepId: 'create', retryLimit: 3 },
            { reference: '$components.failureActions.giveUp' },
          ],
        },
        { stepId: 'confirm', dependsOn: ['create'] },
      ],
    }
    const document = documentWith(workflow, { failureActions: { giveUp: { name: 'giveUp', type: 'end' } } })

    // Act
    const graph = buildWorkflowGraph(document, workflow)

    // Assert
    expect(graph.nodes.map((node) => node.id)).toEqual(['create', 'confirm', SUCCESS_NODE_ID, FAILURE_NODE_ID])
    expect(graph.edges).toEqual([
      { from: 'create', to: 'confirm', kind: 'next', label: '$statusCode == 201' },
      { from: 'create', to: SUCCESS_NODE_ID, kind: 'success', label: '$response.body#/done == true', action: 'done' },
      // Actions without criteria are the catch-all: "otherwise", with the action's name kept for the tooltip.
      { from: 'create', to: 'create', kind: 'failure', label: 'otherwise — retry, ≤ 3×', action: 'again' },
      { from: 'create', to: FAILURE_NODE_ID, kind: 'failure', label: 'otherwise', action: 'giveUp' },
      { from: 'confirm', to: SUCCESS_NODE_ID, kind: 'success' },
    ])
  })

  it('does not repeat a dependsOn arrow an action already draws', () => {
    // Arrange
    const workflow: ArazzoWorkflow = {
      workflowId: 'wf',
      steps: [
        { stepId: 'a', onSuccess: [{ name: 'next', type: 'goto', stepId: 'b' }] },
        { stepId: 'b', dependsOn: ['a'] },
      ],
    }

    // Act
    const graph = buildWorkflowGraph(documentWith(workflow), workflow)

    // Assert
    expect(graph.edges).toEqual([
      { from: 'a', to: 'b', kind: 'success', label: 'otherwise', action: 'next' },
      { from: 'b', to: SUCCESS_NODE_ID, kind: 'success' },
    ])
  })

  it('returns an empty graph for a workflow without steps', () => {
    // Arrange / Act / Assert
    expect(buildWorkflowGraph(documentWith({ workflowId: 'wf' }), undefined)).toEqual({ nodes: [], edges: [], groups: [] })
  })
})

describe('layoutWorkflowGraph', () => {
  it('places the steps top-down and routes every arrow', () => {
    // Arrange
    const workflow: ArazzoWorkflow = {
      workflowId: 'wf',
      steps: [
        { stepId: 'first', onFailure: [{ name: 'stop', type: 'end', criteria: [{ condition: 'x'.repeat(80) }] }] },
        { stepId: 'second' },
      ],
    }

    // Act
    const layout = layoutWorkflowGraph(buildWorkflowGraph(documentWith(workflow), workflow))

    // Assert
    const [first, second] = layout.nodes
    expect(first!.y).toBeLessThan(second!.y)
    expect(layout.width).toBeGreaterThan(0)
    expect(layout.height).toBeGreaterThan(second!.y)
    expect(layout.edges.every((edge) => edge.points.length >= 2)).toBe(true)
    const labeled = layout.edges.find((edge) => edge.label)!
    expect(labeled.label!.length).toBe(MAX_EDGE_LABEL_LENGTH)
    expect(labeled.label!.endsWith('…')).toBe(true)
    expect(labeled.labelPosition).toBeDefined()
  })
})

describe('pan and zoom', () => {
  it('zooms around the pivot and stops at the limits', () => {
    // Arrange
    const pivot = { x: 100, y: 50 }

    // Act
    const zoomed = zoomAt(INITIAL_PAN_ZOOM, 'in', pivot)
    let maxed = INITIAL_PAN_ZOOM
    for (let index = 0; index < 50; index++) {
      maxed = zoomAt(maxed, 'in', pivot)
    }

    // Assert
    expect(zoomed.scale).toBeCloseTo(1.1)
    // The pivot stays put: (pivot - offset) / scale is the same point of the canvas before and after.
    expect((pivot.x - zoomed.x) / zoomed.scale).toBeCloseTo(pivot.x)
    expect(maxed.scale).toBe(MAX_SCALE)
    expect(zoomAt(maxed, 'in', pivot)).toBe(maxed)
  })

  it('pans by the pointer movement', () => {
    // Arrange / Act / Assert
    expect(panBy(INITIAL_PAN_ZOOM, 5, -3)).toEqual({ x: 5, y: -3, scale: 1 })
  })
})
