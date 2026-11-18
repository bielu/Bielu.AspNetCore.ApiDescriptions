import { describe, expect, it } from 'vitest'
import type { PagesConfig } from '../src/helpers/pages'
import {
  buildWorkflowGraph,
  groupId,
  type LaidOutBand,
  layoutWorkflowGraph,
  MAX_BAND_LABEL_LENGTH,
  type WorkflowLayout,
} from '../src/helpers/workflow-graph'
import type { ArazzoDocument, ArazzoWorkflow } from '../src/types'

const config: PagesConfig = { pagesExtension: 'x-pages', pageExtension: 'x-page' }
const route = '/checkout/{orderId}/payment/card-details/confirm'

/** Checkout: a short `pay` step on a page with a long route, beside a failure branch from the cart. */
const checkout = {
  workflowId: 'checkout',
  steps: [
    { stepId: 'cart', 'x-page': 'cart', onFailure: [{ name: 'recover', type: 'goto', stepId: 'recoverCart' }] },
    { stepId: 'pay', 'x-page': 'payment', onSuccess: [{ name: 'paid', type: 'end' }] },
    { stepId: 'recoverCart' },
  ],
} as unknown as ArazzoWorkflow

const shop: ArazzoWorkflow = { workflowId: 'shop', steps: [{ stepId: 'buy', workflowId: 'checkout' }, { stepId: 'track' }] }

const documentWith = (pages: Record<string, { title: string; route?: string }>): ArazzoDocument =>
  ({ arazzo: '1.1.0', 'x-pages': pages, workflows: [shop, checkout] }) as ArazzoDocument

const document = documentWith({ cart: { title: 'Cart' }, payment: { title: 'Payment', route } })

type Rect = { x: number; y: number; width: number; height: number }
const left = (box: Rect) => box.x - box.width / 2
const right = (box: Rect) => box.x + box.width / 2
const top = (box: Rect) => box.y - box.height / 2
const bottom = (box: Rect) => box.y + box.height / 2
const overlaps = (a: Rect, b: Rect) => left(a) < right(b) && left(b) < right(a) && top(a) < bottom(b) && top(b) < bottom(a)

const bandOf = (layout: WorkflowLayout, step: string): LaidOutBand => layout.bands.find((band) => band.nodes.some((id) => id.endsWith(step)))!

const expanded = () => {
  const graph = buildWorkflowGraph(document, shop, undefined, { view: 'pages', pages: config, expanded: new Set(['buy']) })
  return layoutWorkflowGraph(graph)
}

describe('page band geometry', () => {
  it('fits a long page title over a short step inside its expanded workflow', () => {
    // Act
    const layout = expanded()

    // Assert
    const band = bandOf(layout, 'pay')
    const group = layout.groups.find((candidate) => candidate.id === groupId('buy'))!
    expect(band.label).toBe(`Payment · ${route}`)
    expect(band.width).toBeGreaterThanOrEqual(band.label.length * 6 + 16)
    expect(left(band)).toBeGreaterThanOrEqual(left(group))
    expect(right(band)).toBeLessThanOrEqual(right(group))
    expect(bottom(band)).toBeLessThanOrEqual(bottom(group))
  })

  it('keeps the neighbouring branch outside the band', () => {
    // Act
    const layout = expanded()

    // Assert
    const band = bandOf(layout, 'pay')
    const neighbour = layout.nodes.find((node) => node.id === 'buy/recoverCart')!
    expect(overlaps(band, neighbour)).toBe(false)
  })

  it('keeps every band inside the diagram', () => {
    // Act
    const layout = expanded()

    // Assert
    for (const band of layout.bands) {
      expect(left(band), band.id).toBeGreaterThanOrEqual(0)
      expect(top(band), band.id).toBeGreaterThanOrEqual(0)
      expect(right(band), band.id).toBeLessThanOrEqual(layout.width)
      expect(bottom(band), band.id).toBeLessThanOrEqual(layout.height)
    }
  })

  it('draws the step box at its own width, centered in its band, with arrows ending on it', () => {
    // Act
    const layout = expanded()

    // Assert
    const pay = layout.nodes.find((node) => node.id === 'buy/pay')!
    const band = bandOf(layout, 'pay')
    expect(pay.width).toBeLessThan(band.width - 16)
    expect(pay.x).toBeCloseTo(band.x)
    for (const edge of layout.edges.filter((candidate) => candidate.to === 'buy/pay')) {
      const end = edge.points[edge.points.length - 1]!
      expect(end.x).toBeGreaterThanOrEqual(left(pay))
      expect(end.x).toBeLessThanOrEqual(right(pay))
    }
  })

  it('cuts a very long title and route, keeping the band\'s page and route for the tooltip', () => {
    // Arrange
    const longRoute = `/checkout/${'very-long-segment/'.repeat(10)}confirm`
    const workflow = documentWith({ cart: { title: 'Cart' }, payment: { title: 'Payment', route: longRoute } })
    const graph = buildWorkflowGraph(workflow, checkout, undefined, { view: 'pages', pages: config })

    // Act
    const band = bandOf(layoutWorkflowGraph(graph), 'pay')

    // Assert
    expect(band.label).toHaveLength(MAX_BAND_LABEL_LENGTH)
    expect(band.label.endsWith('…')).toBe(true)
    expect(band.route).toBe(longRoute)
  })
})
