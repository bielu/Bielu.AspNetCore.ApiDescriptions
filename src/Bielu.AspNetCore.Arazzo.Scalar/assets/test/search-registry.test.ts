import { beforeEach, describe, expect, it, vi } from 'vitest'
import type { WorkflowView } from '../src/helpers/navigation'
import type { ArazzoDocument, ArazzoStep } from '../src/types'

const calls = vi.hoisted(() => ({ registry: 0 }))
vi.mock('../src/helpers/pages', async (original) => {
  const actual = await original<typeof import('../src/helpers/pages')>()
  return {
    ...actual,
    pageRegistry: (...args: Parameters<typeof actual.pageRegistry>) => {
      calls.registry++
      return actual.pageRegistry(...args)
    },
  }
})

const { filterWorkflows } = await import('../src/helpers/search')

const config = { pagesExtension: 'x-pages', pageExtension: 'x-page', labelExtension: 'x-label' }

/** A workflow with `count` steps on `count` pages; step 7 is the only one on the "Receipt" page. */
const documentWith = (count: number) => {
  const steps = Array.from({ length: count }, (_, index) => ({ stepId: `s${index}`, 'x-page': `p${index}` }) as unknown as ArazzoStep)
  const pages = Object.fromEntries(steps.map((_, index) => [`p${index}`, { title: index === 7 ? 'Receipt' : `Page ${index}` }]))
  const document = { arazzo: '1.1.0', 'x-pages': pages, workflows: [{ workflowId: 'wf', steps }] } as unknown as ArazzoDocument
  const views: WorkflowView[] = [{ id: 'wf', workflow: document.workflows![0]!, steps: steps.map((step) => ({ id: `wf/${step.stepId}`, step })) }]
  return { document, views }
}

beforeEach(() => {
  calls.registry = 0
})

describe('searching with the Pages view configured', () => {
  it('reads the page map once per search, however many steps there are, with the same matches', () => {
    // Arrange
    const small = documentWith(10)
    const large = documentWith(500)

    // Act
    const smallMatches = filterWorkflows(small.views, 'receipt', { document: small.document, config })
    const smallCalls = calls.registry
    calls.registry = 0
    const largeMatches = filterWorkflows(large.views, 'receipt', { document: large.document, config })

    // Assert
    expect(smallCalls).toBe(1)
    expect(calls.registry).toBe(1)
    expect([...smallMatches!.steps]).toEqual(['wf/s7'])
    expect([...largeMatches!.steps]).toEqual(['wf/s7'])
  })

  it('does no Pages work when no Pages option is set', () => {
    // Arrange
    const { document, views } = documentWith(50)

    // Act
    const matches = filterWorkflows(views, 'receipt', { document, config: { endLabelExtension: 'x-end' } })

    // Assert
    expect(calls.registry).toBe(0)
    expect(matches!.matchingSteps).toBe(0)
  })
})
