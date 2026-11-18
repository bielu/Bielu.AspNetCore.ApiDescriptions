import { describe, expect, it } from 'vitest'
import { toWorkflowViews, traverseArazzoDocument } from '../src/helpers/navigation'
import type { PagesConfig } from '../src/helpers/pages'
import { filterWorkflows } from '../src/helpers/search'
import type { ArazzoDocument } from '../src/types'

const config: PagesConfig = { pagesExtension: 'x-pages', pageExtension: 'x-page', labelExtension: 'x-label' }

/** A checkout whose steps are named only in the Pages extensions: their ids say nothing a shopper would type. */
const document = {
  arazzo: '1.1.0',
  'x-pages': { payment: { title: 'Card payment', route: '/checkout/payment' }, done: { title: 'Thank you', route: '/checkout/done' } },
  workflows: [
    {
      workflowId: 'checkout',
      'x-label': 'Buy the basket',
      failureActions: [{ name: 'declined', type: 'end', 'x-page': 'declinedPage', 'x-label': 'Card refused' }],
      steps: [
        { stepId: 's1', 'x-label': 'Review the basket' },
        { stepId: 's2', 'x-page': 'payment' },
        { stepId: 's3', onSuccess: [{ name: 'finish', type: 'end', 'x-page': 'done' }] },
      ],
    },
    { workflowId: 'browse', steps: [{ stepId: 's1' }] },
  ],
} as unknown as ArazzoDocument

const views = toWorkflowViews(document, traverseArazzoDocument({ documentId: 'd', document: document as unknown as Record<string, unknown> }))
const pages = { document, config }
const stepIds = (query: string) => [...(filterWorkflows(views, query, pages)?.steps ?? [])]

describe('searching what the Pages view shows', () => {
  it('finds a step by its page\'s route, and counts only it', () => {
    // Act
    const filter = filterWorkflows(views, '/checkout/payment', pages)!

    // Assert
    expect([...filter.steps]).toEqual(['d/workflow/checkout/step/s2'])
    expect(filter.matchingSteps).toBe(1)
    expect(filter.totalSteps).toBe(4)
  })

  it('finds a step by its page title or its label', () => {
    // Act / Assert
    expect(stepIds('card payment')).toEqual(['d/workflow/checkout/step/s2'])
    expect(stepIds('review basket')).toEqual(['d/workflow/checkout/step/s1'])
  })

  it('finds a step by the page or label of an action it runs, its workflow\'s included', () => {
    // Act / Assert
    expect(stepIds('thank you')).toEqual(['d/workflow/checkout/step/s3'])
    expect(stepIds('card refused')).toEqual(['d/workflow/checkout/step/s1', 'd/workflow/checkout/step/s2', 'd/workflow/checkout/step/s3'])
  })

  it('finds a workflow by its label, keeping all its steps', () => {
    // Act
    const filter = filterWorkflows(views, 'buy the basket', pages)!

    // Assert
    expect([...filter.workflows]).toEqual(['d/workflow/checkout'])
    expect(filter.matchingSteps).toBe(3)
  })

  it('ignores the extensions when the Pages view is not configured', () => {
    // Act / Assert
    expect(filterWorkflows(views, '/checkout/payment')!.matchingSteps).toBe(0)
    expect(filterWorkflows(views, 'review', { document, config: {} })!.matchingSteps).toBe(0)
  })
})
