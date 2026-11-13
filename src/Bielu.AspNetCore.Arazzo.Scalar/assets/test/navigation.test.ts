import { describe, expect, it } from 'vitest'
import { isArazzoDocument, toWorkflowViews, traverseArazzoDocument } from '../src/helpers/navigation'
import type { ArazzoDocument } from '../src/types'

const document: ArazzoDocument = {
  arazzo: '1.0.1',
  info: { title: 'Pet adoption', version: '1.0.0' },
  workflows: [
    {
      workflowId: 'adopt',
      summary: 'Adopt a pet',
      steps: [{ stepId: 'find' }, { stepId: 'reserve' }],
    },
    { workflowId: 'cancel' },
  ],
}

describe('isArazzoDocument', () => {
  it('claims a document with an arazzo version and workflows', () => {
    // Arrange / Act / Assert
    expect(isArazzoDocument(document as unknown as Record<string, unknown>)).toBe(true)
  })

  it.each([
    { openapi: '3.1.0', paths: {} },
    { asyncapi: '3.0.0' },
    { arazzo: 1, workflows: [] },
    { arazzo: '1.0.1' },
  ])('leaves other documents to the built-in pipelines: %j', (candidate) => {
    // Arrange / Act / Assert
    expect(isArazzoDocument(candidate)).toBe(false)
  })
})

describe('traverseArazzoDocument', () => {
  it('lists workflows with their steps, prefixed with the document id', () => {
    // Arrange / Act
    const entries = traverseArazzoDocument({ documentId: 'doc', document: document as unknown as Record<string, unknown> })

    // Assert
    expect(entries).toEqual([
      {
        id: 'doc/workflow/adopt',
        title: 'Adopt a pet',
        kind: 'workflow',
        children: [
          { id: 'doc/workflow/adopt/step/find', title: 'find', kind: 'step' },
          { id: 'doc/workflow/adopt/step/reserve', title: 'reserve', kind: 'step' },
        ],
      },
      { id: 'doc/workflow/cancel', title: 'cancel', kind: 'workflow', children: [] },
    ])
  })

  it('returns no entries for a document without workflows', () => {
    // Arrange / Act
    const entries = traverseArazzoDocument({ documentId: 'doc', document: { arazzo: '1.0.1' } })

    // Assert
    expect(entries).toEqual([])
  })
})

describe('toWorkflowViews', () => {
  it('pairs workflows and steps with the navigation entries by position', () => {
    // Arrange
    const navigation = traverseArazzoDocument({ documentId: 'x', document: document as unknown as Record<string, unknown> })

    // Act
    const views = toWorkflowViews(document, navigation)

    // Assert
    expect(views.map((view) => view.id)).toEqual(['x/workflow/adopt', 'x/workflow/cancel'])
    expect(views[0]!.steps.map((step) => [step.id, step.step.stepId])).toEqual([
      ['x/workflow/adopt/step/find', 'find'],
      ['x/workflow/adopt/step/reserve', 'reserve'],
    ])
  })

  it('drops workflows Scalar has no entry for', () => {
    // Arrange / Act
    const views = toWorkflowViews(document, [])

    // Assert
    expect(views).toEqual([])
  })
})
