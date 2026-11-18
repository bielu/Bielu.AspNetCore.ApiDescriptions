import { describe, expect, it } from 'vitest'
import { pagesInUse, type PagesConfig } from '../src/helpers/pages'
import { buildWorkflowGraph, FAILURE_NODE_ID, SUCCESS_NODE_ID } from '../src/helpers/workflow-graph'
import type { ArazzoDocument, ArazzoStep, ArazzoWorkflow } from '../src/types'

/** A checkout: paid orders land on Confirmation, pending ones on Review, and a rejection on a page per step. */
const config: PagesConfig = { pagesExtension: 'x-pages', pageExtension: 'x-page', labelExtension: 'x-label' }

const pay = {
  stepId: 'pay',
  onSuccess: [
    { name: 'paid', type: 'end', criteria: [{ condition: '$response.body#/status == "paid"' }], 'x-page': 'confirmation' },
    { name: 'pending', type: 'end', criteria: [{ condition: '$response.body#/status == "pending"' }], 'x-page': 'review' },
  ],
  onFailure: [{ name: 'rejected', type: 'end', 'x-page': 'paymentFailed', 'x-label': 'Card declined' }],
} as unknown as ArazzoStep

const reserve = {
  stepId: 'reserve',
  onFailure: [{ name: 'rejected', type: 'end', 'x-page': 'outOfStock', 'x-label': 'Sold out' }],
} as unknown as ArazzoStep

const notify: ArazzoStep = { stepId: 'notify' }

const documentWith = (workflow: ArazzoWorkflow): ArazzoDocument =>
  ({
    arazzo: '1.1.0',
    'x-pages': {
      confirmation: { title: 'Confirmation' },
      review: { title: 'Review' },
      paymentFailed: { title: 'Payment failed' },
      outOfStock: { title: 'Out of stock' },
    },
    workflows: [workflow],
  }) as ArazzoDocument

const pages = (steps: ArazzoStep[]) => {
  const workflow: ArazzoWorkflow = { workflowId: 'checkout', steps }
  return buildWorkflowGraph(documentWith(workflow), workflow, undefined, { view: 'pages', pages: config })
}

/** Where each arrow into an end node goes, and the page it names. */
const landings = (graph: ReturnType<typeof pages>) =>
  graph.edges
    .filter((edge) => edge.to === SUCCESS_NODE_ID || edge.to === FAILURE_NODE_ID)
    .map(({ from, to, action, page }) => ({ from, to, ...(action ? { action } : {}), ...(page ? { page } : {}) }))

describe('landing pages in the Pages view', () => {
  it('keeps each success outcome on its own page when they differ, and the Success node generic', () => {
    // Act
    const graph = pages([reserve, notify, pay])

    // Assert
    expect(graph.nodes.find((node) => node.id === SUCCESS_NODE_ID)).toMatchObject({ label: 'Success' })
    expect(graph.nodes.find((node) => node.id === SUCCESS_NODE_ID)!.page).toBeUndefined()
    const paid = graph.edges.find((edge) => edge.action === 'paid')!
    const pending = graph.edges.find((edge) => edge.action === 'pending')!
    expect(paid).toMatchObject({ page: 'confirmation' })
    expect(paid.label).toMatch(/→ Confirmation$/)
    expect(pending).toMatchObject({ page: 'review' })
    expect(pending.label).toMatch(/→ Review$/)
  })

  it('keeps an implicit success without a page off every declared page', () => {
    // Act: the last step, notify, ends the workflow without saying where.
    const graph = pages([pay, reserve, notify])

    // Assert
    const implicit = graph.edges.find((edge) => edge.from === 'notify' && edge.to === SUCCESS_NODE_ID)!
    expect(implicit).toMatchObject({ label: 'done' })
    expect(implicit.page).toBeUndefined()
    expect(graph.nodes.find((node) => node.id === SUCCESS_NODE_ID)!.label).toBe('Success')
  })

  it('lands same-named failure actions of different steps on their own pages', () => {
    // Act
    const graph = pages([reserve, pay])

    // Assert
    const failed = graph.nodes.find((node) => node.id === FAILURE_NODE_ID)!
    expect(failed).toMatchObject({ label: 'Failed' })
    expect(failed.subtitle).toBeUndefined()
    expect(landings(graph).filter((landing) => landing.to === FAILURE_NODE_ID)).toEqual([
      { from: 'reserve', to: FAILURE_NODE_ID, action: 'rejected', page: 'outOfStock' },
      { from: 'pay', to: FAILURE_NODE_ID, action: 'rejected', page: 'paymentFailed' },
    ])
  })

  it('keeps every branch\'s destination when the steps are reordered', () => {
    // Arrange
    const destinations = (graph: ReturnType<typeof pages>) =>
      Object.fromEntries(graph.edges.filter((edge) => edge.page).map((edge) => [`${edge.from}:${edge.action}`, edge.page]))

    // Act
    const forward = destinations(pages([reserve, notify, pay]))
    const reversed = destinations(pages([pay, notify, reserve]))

    // Assert
    expect(forward).toEqual({
      'reserve:rejected': 'outOfStock',
      'pay:paid': 'confirmation',
      'pay:pending': 'review',
      'pay:rejected': 'paymentFailed',
    })
    expect(reversed).toEqual(forward)
  })

  it('names a shared end node after its page, with its caption, when every arrow into it agrees', () => {
    // Arrange
    const first = { stepId: 'a', onFailure: [{ name: 'rejected', type: 'end', 'x-page': 'paymentFailed', 'x-label': 'Card declined' }] }
    const second = { stepId: 'b', onFailure: [{ name: 'rejected', type: 'end', 'x-page': 'paymentFailed', 'x-label': 'Card declined' }] }

    // Act
    const graph = pages([first, second] as unknown as ArazzoStep[])

    // Assert
    expect(graph.nodes.find((node) => node.id === FAILURE_NODE_ID)).toMatchObject({ label: 'Payment failed', page: 'paymentFailed', subtitle: 'Card declined' })
    expect(graph.edges.filter((edge) => edge.to === FAILURE_NODE_ID).every((edge) => !edge.page)).toBe(true)
  })

  it('counts a page on an inherited workflow action as use of the Pages view', () => {
    // Arrange
    const workflow = {
      workflowId: 'checkout',
      failureActions: [{ name: 'rejected', type: 'end', 'x-page': 'paymentFailed' }],
      steps: [{ stepId: 'pay' }],
    } as unknown as ArazzoWorkflow

    // Act / Assert
    expect(pagesInUse({ arazzo: '1.1.0', workflows: [workflow] }, { pageExtension: 'x-page' })).toBe(true)
  })
})
