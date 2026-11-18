import { describe, expect, it } from 'vitest'
import { effectiveFailureActions, effectiveSuccessActions } from '../src/helpers/actions'
import { buildWorkflowGraph, FAILURE_NODE_ID, SUCCESS_NODE_ID } from '../src/helpers/workflow-graph'
import type { ArazzoDocument, ArazzoWorkflow } from '../src/types'

const documentWith = (workflow: ArazzoWorkflow): ArazzoDocument => ({
  arazzo: '1.1.0',
  workflows: [workflow],
  components: { failureActions: { giveUp: { name: 'giveUp', type: 'end' } } },
})

const edgesFrom = (graph: ReturnType<typeof buildWorkflowGraph>, from: string) =>
  graph.edges.filter((edge) => edge.from === from).map(({ to, kind, action }) => ({ to, kind, ...(action ? { action } : {}) }))

describe('effective actions', () => {
  it('applies the workflow\'s actions to every step, resolving references, after the step\'s own', () => {
    // Arrange
    const workflow: ArazzoWorkflow = {
      workflowId: 'wf',
      failureActions: [{ reference: '$components.failureActions.giveUp' }],
      successActions: [{ name: 'done', type: 'end', criteria: [{ condition: '$statusCode == 204' }] }],
      steps: [{ stepId: 'a', onFailure: [{ name: 'again', type: 'retry' }] }],
    }
    const document = documentWith(workflow)

    // Act
    const failures = effectiveFailureActions(document, workflow, workflow.steps![0]!)
    const successes = effectiveSuccessActions(document, workflow, workflow.steps![0]!)

    // Assert
    expect(failures.map((action) => action.name)).toEqual(['again', 'giveUp'])
    expect(successes.map((action) => action.name)).toEqual(['done'])
  })

  it('lets a step override a workflow action by name, but not remove the others', () => {
    // Arrange
    const workflow: ArazzoWorkflow = {
      workflowId: 'wf',
      failureActions: [{ reference: '$components.failureActions.giveUp' }, { name: 'other', type: 'end' }],
      steps: [{ stepId: 'a', onFailure: [{ name: 'giveUp', type: 'goto', stepId: 'b' }] }, { stepId: 'b' }],
    }

    // Act
    const failures = effectiveFailureActions(documentWith(workflow), workflow, workflow.steps![0]!)

    // Assert
    expect(failures).toEqual([
      { name: 'giveUp', type: 'goto', stepId: 'b' },
      { name: 'other', type: 'end' },
    ])
  })
})

describe('arrows that follow the effective actions', () => {
  it('draws an inherited action out of every step', () => {
    // Arrange
    const workflow: ArazzoWorkflow = {
      workflowId: 'wf',
      failureActions: [{ reference: '$components.failureActions.giveUp' }],
      steps: [{ stepId: 'a' }, { stepId: 'b' }],
    }

    // Act
    const graph = buildWorkflowGraph(documentWith(workflow), workflow)

    // Assert
    expect(edgesFrom(graph, 'a')).toContainEqual({ to: FAILURE_NODE_ID, kind: 'failure', action: 'giveUp' })
    expect(edgesFrom(graph, 'b')).toContainEqual({ to: FAILURE_NODE_ID, kind: 'failure', action: 'giveUp' })
  })

  it('changes only the overriding step\'s branch', () => {
    // Arrange
    const workflow: ArazzoWorkflow = {
      workflowId: 'wf',
      failureActions: [{ reference: '$components.failureActions.giveUp' }],
      steps: [
        { stepId: 'a', onFailure: [{ name: 'giveUp', type: 'goto', stepId: 'c' }] },
        { stepId: 'b' },
        { stepId: 'c' },
      ],
    }

    // Act
    const graph = buildWorkflowGraph(documentWith(workflow), workflow)

    // Assert
    expect(edgesFrom(graph, 'a').filter((edge) => edge.kind === 'failure')).toEqual([{ to: 'c', kind: 'failure', action: 'giveUp' }])
    expect(edgesFrom(graph, 'b').filter((edge) => edge.kind === 'failure')).toEqual([
      { to: FAILURE_NODE_ID, kind: 'failure', action: 'giveUp' },
    ])
  })

  it('draws one success path out of a step that always jumps, and no arrow on to the next step', () => {
    // Arrange
    const workflow: ArazzoWorkflow = {
      workflowId: 'wf',
      steps: [{ stepId: 'a', onSuccess: [{ name: 'skip', type: 'goto', stepId: 'c' }] }, { stepId: 'b' }, { stepId: 'c' }],
    }

    // Act
    const graph = buildWorkflowGraph(documentWith(workflow), workflow)

    // Assert
    expect(edgesFrom(graph, 'a')).toEqual([{ to: 'c', kind: 'success', action: 'skip' }])
    expect(edgesFrom(graph, 'b')).toEqual([{ to: 'c', kind: 'next' }])
  })

  it('draws no arrow on after an unconditional inherited end, nor an implicit success', () => {
    // Arrange
    const workflow: ArazzoWorkflow = {
      workflowId: 'wf',
      successActions: [{ name: 'finish', type: 'end' }],
      steps: [{ stepId: 'a' }, { stepId: 'b' }],
    }

    // Act
    const graph = buildWorkflowGraph(documentWith(workflow), workflow)

    // Assert
    expect(edgesFrom(graph, 'a')).toEqual([{ to: SUCCESS_NODE_ID, kind: 'success', action: 'finish' }])
    expect(edgesFrom(graph, 'b')).toEqual([{ to: SUCCESS_NODE_ID, kind: 'success', action: 'finish' }])
  })

  it('keeps the arrow on and the implicit success when the success actions have criteria', () => {
    // Arrange
    const workflow: ArazzoWorkflow = {
      workflowId: 'wf',
      steps: [
        { stepId: 'a', onSuccess: [{ name: 'skip', type: 'goto', stepId: 'c', criteria: [{ condition: '$statusCode == 204' }] }] },
        { stepId: 'b' },
        { stepId: 'c', onSuccess: [{ name: 'back', type: 'goto', stepId: 'a', criteria: [{ condition: '$statusCode == 202' }] }] },
      ],
    }

    // Act
    const graph = buildWorkflowGraph(documentWith(workflow), workflow)

    // Assert
    expect(edgesFrom(graph, 'a')).toEqual([
      { to: 'b', kind: 'next' },
      { to: 'c', kind: 'success', action: 'skip' },
    ])
    expect(edgesFrom(graph, 'c')).toEqual([
      { to: 'a', kind: 'success', action: 'back' },
      { to: SUCCESS_NODE_ID, kind: 'success' },
    ])
  })

  it('advances normally on success from a step that only has failure actions', () => {
    // Arrange
    const workflow: ArazzoWorkflow = {
      workflowId: 'wf',
      steps: [{ stepId: 'a', onFailure: [{ name: 'stop', type: 'end' }] }, { stepId: 'b' }],
    }

    // Act
    const graph = buildWorkflowGraph(documentWith(workflow), workflow)

    // Assert
    expect(edgesFrom(graph, 'a')).toContainEqual({ to: 'b', kind: 'next' })
    expect(edgesFrom(graph, 'b')).toEqual([{ to: SUCCESS_NODE_ID, kind: 'success' }])
  })
})

describe('retries', () => {
  it('returns to the retrying step from the step a retry runs first', () => {
    // Arrange
    const workflow: ArazzoWorkflow = {
      workflowId: 'wf',
      steps: [
        { stepId: 'a', onFailure: [{ name: 'refresh', type: 'retry', stepId: 'b', retryLimit: 2 }] },
        { stepId: 'b' },
      ],
    }

    // Act
    const graph = buildWorkflowGraph(documentWith(workflow), workflow)

    // Assert
    expect(graph.edges).toContainEqual({ from: 'a', to: 'b', kind: 'failure', label: 'otherwise — retry, ≤ 2×', action: 'refresh' })
    expect(graph.edges).toContainEqual({ from: 'b', to: 'a', kind: 'return' })
  })

  it('shows a retry of the step itself on the step, with its limit and delay', () => {
    // Arrange
    const workflow: ArazzoWorkflow = {
      workflowId: 'wf',
      steps: [
        {
          stepId: 'a',
          onFailure: [
            { name: 'busy', type: 'retry', retryLimit: 3, retryAfter: 2, criteria: [{ condition: '$statusCode == 503' }] },
          ],
        },
      ],
    }

    // Act
    const graph = buildWorkflowGraph(documentWith(workflow), workflow)

    // Assert
    expect(graph.nodes[0]!.retries).toEqual([
      { label: '$statusCode == 503 — retry, ≤ 3×, after 2s', action: 'busy', retryLimit: 3, retryAfter: 2 },
    ])
    expect(graph.edges.some((edge) => edge.from === 'a' && edge.to === 'a')).toBe(false)
  })
})
