import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'
import { schemaPropertyRows } from '../src/helpers/format'
import { toWorkflowViews, traverseArazzoDocument } from '../src/helpers/navigation'
import { normalizeArazzoDocument } from '../src/helpers/normalize'
import { INITIAL_PAN_ZOOM, KEYBOARD_PAN_STEP, panZoomForKey } from '@bielu/scalar-graph'
import { resolveStepTarget } from '../src/helpers/resolve-target'
import { countStepsBySpec, summarizeSources } from '../src/helpers/target-spec'
import { buildWorkflowGraph, layoutWorkflowGraph } from '../src/helpers/workflow-graph'
import type { ArazzoDocument, ArazzoStep } from '../src/types'

// Documents come from user input; Scalar only checks the `arazzo` field before handing them over.
const malformed: unknown[] = [
  {
    arazzo: '1.0.0',
    workflows: [
      null,
      1,
      'x',
      { workflowId: 'a', steps: [null, 2, {}, { stepId: 's', dependsOn: 'nope', onSuccess: [null, 'x'], onFailure: {} }] },
    ],
  },
  { arazzo: '1.0.0', workflows: [{ workflowId: 'a', steps: 'nope' }], sourceDescriptions: [null, 'x', { name: 1 }] },
  {
    arazzo: 1,
    info: 'x',
    workflows: [{ workflowId: 'a', inputs: 'x', steps: [{ stepId: 's', operationId: 5, successCriteria: [null, { condition: 1 }] }] }],
    components: 'x',
  },
  { arazzo: '1.1.0', workflows: [{ workflowId: 7 }, { workflowId: 'b', steps: [{ stepId: 's', requestBody: { replacements: 'x' } }] }] },
]

beforeEach(() => {
  vi.stubGlobal('location', { href: 'https://app.test/' })
})

afterEach(() => {
  vi.unstubAllGlobals()
})

describe('normalizeArazzoDocument', () => {
  it.each(malformed.map((document, index) => [index, document]))('renders malformed document %i without throwing', (_, raw) => {
    // Arrange
    const navigation = traverseArazzoDocument({ documentId: 'd', document: raw as Record<string, unknown> })
    const document = normalizeArazzoDocument(raw)!

    // Act
    const render = () => {
      toWorkflowViews(document, navigation)
      summarizeSources(document, {})
      countStepsBySpec(document, {})
      for (const workflow of document.workflows ?? []) {
        layoutWorkflowGraph(buildWorkflowGraph(document, workflow))
        schemaPropertyRows(workflow.inputs)
        for (const step of workflow.steps ?? []) {
          resolveStepTarget(document, step, {})
        }
      }
    }

    // Assert
    expect(render).not.toThrow()
  })

  it('keeps well-formed entries and drops the rest', () => {
    // Arrange / Act
    const document = normalizeArazzoDocument(malformed[0])!
    const workflow = document.workflows![0]!
    const step = workflow.steps![0]!

    // Assert
    expect(document.workflows!.map((entry) => entry.workflowId)).toEqual(['a'])
    expect(workflow.steps!.map((entry) => entry.stepId)).toEqual(['s'])
    expect(step.dependsOn).toBeUndefined()
    expect(step.onSuccess).toEqual([])
    expect(step.onFailure).toBeUndefined()
  })

  it('turns non-text fields into nothing rather than rendering them', () => {
    // Arrange / Act
    const document = normalizeArazzoDocument(malformed[2])!

    // Assert
    expect(document.arazzo).toBe('')
    expect(document.info).toBeUndefined()
    expect(document.components).toBeUndefined()
    expect(document.workflows![0]!.steps![0]!.operationId).toBeUndefined()
    expect(document.workflows![0]!.steps![0]!.successCriteria).toEqual([])
  })

  it('keeps component maps and extensions, and drops component kinds that are not maps', () => {
    // Arrange / Act
    const document = normalizeArazzoDocument({
      arazzo: '1.1.0',
      workflows: [],
      components: { parameters: { a: { name: 'a' } }, inputs: 'x', 'x-note': 1 },
    })!

    // Assert
    expect(document.components).toEqual({ parameters: { a: { name: 'a' } }, 'x-note': 1 })
  })

  it('returns the same copy for the same document, so the sidebar and the content line up', () => {
    // Arrange
    const raw = { arazzo: '1.1.0', workflows: [{ workflowId: 'a' }] }

    // Act / Assert
    expect(normalizeArazzoDocument(raw)).toBe(normalizeArazzoDocument(raw))
    expect(normalizeArazzoDocument(null)).toBeUndefined()
    expect(normalizeArazzoDocument([])).toBeUndefined()
  })
})

describe('panZoomForKey', () => {
  const center = { x: 100, y: 50 }

  it('pans the way the arrow points', () => {
    // Arrange / Act / Assert
    expect(panZoomForKey(INITIAL_PAN_ZOOM, 'ArrowLeft', center)).toEqual({ x: KEYBOARD_PAN_STEP, y: 0, scale: 1 })
    expect(panZoomForKey(INITIAL_PAN_ZOOM, 'ArrowRight', center)).toEqual({ x: -KEYBOARD_PAN_STEP, y: 0, scale: 1 })
    expect(panZoomForKey(INITIAL_PAN_ZOOM, 'ArrowUp', center)).toEqual({ x: 0, y: KEYBOARD_PAN_STEP, scale: 1 })
    expect(panZoomForKey(INITIAL_PAN_ZOOM, 'ArrowDown', center)).toEqual({ x: 0, y: -KEYBOARD_PAN_STEP, scale: 1 })
  })

  it('zooms around the center and resets', () => {
    // Arrange
    const zoomedIn = panZoomForKey(INITIAL_PAN_ZOOM, '+', center)!

    // Act / Assert
    expect(zoomedIn.scale).toBeGreaterThan(1)
    expect(panZoomForKey(INITIAL_PAN_ZOOM, '=', center)).toEqual(zoomedIn)
    expect(panZoomForKey(zoomedIn, '-', center)!.scale).toBeCloseTo(1)
    expect(panZoomForKey(zoomedIn, '0', center)).toEqual(INITIAL_PAN_ZOOM)
  })

  it('leaves other keys to the page', () => {
    // Arrange / Act / Assert
    expect(panZoomForKey(INITIAL_PAN_ZOOM, 'Tab', center)).toBeUndefined()
    expect(panZoomForKey(INITIAL_PAN_ZOOM, 'a', center)).toBeUndefined()
  })
})

describe('large workflows', () => {
  it('lays out a 300-step workflow with branches quickly', () => {
    // Arrange
    const steps: ArazzoStep[] = Array.from({ length: 300 }, (_, index) => ({
      stepId: `step${index}`,
      operationId: `op${index}`,
      dependsOn: index > 0 ? [`step${index - 1}`] : undefined,
      onFailure: index % 10 === 5 ? [{ name: 'retry', type: 'retry', stepId: `step${index - 5}`, retryLimit: 3 }] : undefined,
      onSuccess: index % 50 === 49 ? [{ name: 'done', type: 'end' }] : undefined,
    }))
    const document: ArazzoDocument = { arazzo: '1.1.0', workflows: [{ workflowId: 'big', steps }] }

    // Act
    const started = performance.now()
    const layout = layoutWorkflowGraph(buildWorkflowGraph(document, document.workflows![0]))
    const elapsed = performance.now() - started

    // Assert
    expect(layout.nodes.filter((node) => node.kind === 'step')).toHaveLength(300)
    // Generous for slow CI machines; locally this takes a fraction of it.
    expect(elapsed).toBeLessThan(3000)
  })
})
