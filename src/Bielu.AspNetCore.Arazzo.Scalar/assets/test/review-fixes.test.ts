import { describe, expect, it } from 'vitest'
import { resolvePluginOptions, pluginOptionsFromModuleUrl } from '../src/helpers/plugin-options'
import {
  buildWorkflowGraph,
  FAILURE_NODE_ID,
  groupId,
  layoutWorkflowGraph,
  MAX_EDGE_LABEL_LENGTH,
  SUCCESS_NODE_ID,
} from '../src/helpers/workflow-graph'
import type { ArazzoDocument, ArazzoFailureAction, ArazzoStep, ArazzoWorkflow } from '../src/types'

const documentWith = (...workflows: ArazzoWorkflow[]): ArazzoDocument => ({ arazzo: '1.1.0', workflows })

const outgoing = (graph: ReturnType<typeof buildWorkflowGraph>, from: string) =>
  graph.edges.filter((edge) => edge.from === from).map(({ to, kind }) => `${kind}:${to}`)

describe('plugin options', () => {
  it('ignores an end-label extension that is not "x-" followed by a name, falling through to the next source', () => {
    // Act / Assert
    expect(resolvePluginOptions({ endLabelExtension: 'type' }, { endLabelExtension: 'x-page' })).toEqual({ endLabelExtension: 'x-page' })
    expect(resolvePluginOptions({ endLabelExtension: 'x-' }, undefined, 'https://cdn.test/p.mjs?endLabelExtension=x-screen')).toEqual({
      endLabelExtension: 'x-screen',
    })
    expect(pluginOptionsFromModuleUrl('https://cdn.test/p.mjs?endLabelExtension=type')).toEqual({})
  })
})

describe('end nodes', () => {
  it('never merges an extension value with an action whose name looks like one', () => {
    // Arrange: one action ends on x-page "blocked", the other is merely named "x-page=blocked".
    const workflow: ArazzoWorkflow = {
      workflowId: 'wf',
      steps: [
        {
          stepId: 'a',
          onFailure: [
            { name: 'locked', type: 'end', criteria: [{ condition: '$statusCode == 423' }], 'x-page': 'blocked' } as ArazzoFailureAction,
            { name: 'x-page=blocked', type: 'end' },
          ],
        },
      ],
    }

    // Act
    const graph = buildWorkflowGraph(documentWith(workflow), workflow, undefined, { endLabelExtension: 'x-page' })

    // Assert
    const ends = graph.nodes.filter((node) => node.kind === 'failure')
    expect(ends.map((node) => node.label)).toEqual(['blocked', 'x-page=blocked'])
    expect(new Set(graph.edges.filter((edge) => edge.kind === 'failure').map((edge) => edge.to)).size).toBe(2)
  })

  it('gives the implicit Success an id no step can have', () => {
    // Arrange: a step whose id is the old synthetic Success id.
    const workflow: ArazzoWorkflow = { workflowId: 'wf', steps: [{ stepId: '__workflow_success__' }] }

    // Act
    const graph = buildWorkflowGraph(documentWith(workflow), workflow)

    // Assert
    expect(graph.nodes.map((node) => node.id)).toEqual(['__workflow_success__', SUCCESS_NODE_ID])
    expect(new Set(graph.nodes.map((node) => node.id)).size).toBe(2)
    expect(SUCCESS_NODE_ID).toContain(':')
    expect(FAILURE_NODE_ID).toContain(':')
  })
})

describe('actions that can never run', () => {
  it('draws no branch after an unconditional success action, keeping the conditional ones before it', () => {
    // Arrange: the step ends unconditionally, and inherits a differently named conditional goto it can never reach.
    const workflow: ArazzoWorkflow = {
      workflowId: 'wf',
      successActions: [{ name: 'skip', type: 'goto', stepId: 'c', criteria: [{ condition: '$statusCode == 202' }] }],
      steps: [
        {
          stepId: 'a',
          onSuccess: [
            { name: 'retryLater', type: 'goto', stepId: 'b', criteria: [{ condition: '$statusCode == 204' }] },
            { name: 'done', type: 'end' },
          ],
        },
        { stepId: 'b' },
        { stepId: 'c' },
      ],
    }

    // Act
    const graph = buildWorkflowGraph(documentWith(workflow), workflow)

    // Assert
    expect(outgoing(graph, 'a')).toEqual(['success:b', `success:${SUCCESS_NODE_ID}`])
  })

  it('draws no next arrow from a dependency whose success always ends the workflow', () => {
    // Arrange
    const workflow: ArazzoWorkflow = {
      workflowId: 'wf',
      steps: [
        { stepId: 'a', onSuccess: [{ name: 'done', type: 'end' }] },
        { stepId: 'b', dependsOn: ['a'] },
        { stepId: 'c', dependsOn: ['b'] },
      ],
    }

    // Act
    const graph = buildWorkflowGraph(documentWith(workflow), workflow)

    // Assert
    expect(outgoing(graph, 'a')).toEqual([`success:${SUCCESS_NODE_ID}`])
    expect(outgoing(graph, 'b')).toEqual(['next:c'])
  })
})

describe('layout', () => {
  it('routes a step\'s goto back to itself', () => {
    // Arrange
    const workflow: ArazzoWorkflow = {
      workflowId: 'wf',
      steps: [{ stepId: 'poll', onSuccess: [{ name: 'again', type: 'goto', stepId: 'poll', criteria: [{ condition: '$response.body#/pending' }] }] }],
    }

    // Act
    const layout = layoutWorkflowGraph(buildWorkflowGraph(documentWith(workflow), workflow))

    // Assert
    const loop = layout.edges.find((edge) => edge.from === 'poll' && edge.to === 'poll')!
    expect(loop.points.length).toBeGreaterThan(1)
  })

  it('makes an expanded workflow\'s group wide enough for its label, cutting a very long one', () => {
    // Arrange: a short step calls a workflow with a long id and one short step.
    const long: ArazzoWorkflow = { workflowId: 'synchroniseLoyaltyAccountWithWarehouse', steps: [{ stepId: 'go' }] }
    const workflow: ArazzoWorkflow = { workflowId: 'wf', steps: [{ stepId: 'call', workflowId: long.workflowId } as ArazzoStep] }
    const longer: ArazzoWorkflow = { ...long, workflowId: `${long.workflowId}AndThenTellEveryoneAboutIt` }
    const viaLonger: ArazzoWorkflow = { workflowId: 'wf', steps: [{ stepId: 'call', workflowId: longer.workflowId } as ArazzoStep] }

    // Act
    const layout = layoutWorkflowGraph(buildWorkflowGraph(documentWith(workflow, long), workflow, undefined, { expanded: new Set(['call']) }))
    const cut = layoutWorkflowGraph(buildWorkflowGraph(documentWith(viaLonger, longer), viaLonger, undefined, { expanded: new Set(['call']) }))

    // Assert
    const group = layout.groups.find((candidate) => candidate.id === groupId('call'))!
    expect(group.width).toBeGreaterThanOrEqual(10 + long.workflowId.length * 6.7)
    expect(layout.width).toBeGreaterThanOrEqual(group.x + group.width / 2)
    const cutGroup = cut.groups.find((candidate) => candidate.id === groupId('call'))!
    expect(cutGroup.width).toBeLessThan(10 + longer.workflowId.length * 6.7)
    expect(cutGroup.width).toBeGreaterThanOrEqual(10 + MAX_EDGE_LABEL_LENGTH * 6.7)
  })
})
