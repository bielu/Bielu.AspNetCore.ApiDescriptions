import { describe, expect, it } from 'vitest'
import { buildDataFlowGraph } from '../src/helpers/data-flow'
import { layoutWorkflowGraph, uncross, type WorkflowLayout } from '../src/helpers/workflow-graph'
import type { ArazzoDocument, ArazzoWorkflow } from '../src/types'

type Point = { x: number; y: number }

/** Whether two segments cross at a point inside both (touching at an end, as arrows into one box do, is not crossing). */
const crosses = (a: Point, b: Point, c: Point, d: Point): boolean => {
  const side = (p: Point, q: Point, r: Point) => Math.sign((q.x - p.x) * (r.y - p.y) - (q.y - p.y) * (r.x - p.x))
  return side(a, b, c) * side(a, b, d) < 0 && side(c, d, a) * side(c, d, b) < 0
}

/** How many pairs of arrows cross in the laid-out overview. */
const crossings = (layout: WorkflowLayout): number => {
  const segments = layout.edges.map((edge) => edge.points.slice(1).map((point, index) => [edge.points[index]!, point] as const))
  let count = 0
  for (let i = 0; i < segments.length; i++) {
    for (let j = i + 1; j < segments.length; j++) {
      if (segments[i]!.some(([a, b]) => segments[j]!.some(([c, d]) => crosses(a, b, c, d)))) {
        count++
      }
    }
  }
  return count
}

/** The ArazzoScalar example's checkout: a step, then three workflows fed by it and by each other's outputs. */
const checkout: ArazzoWorkflow = {
  workflowId: 'checkout',
  steps: [
    { stepId: 'create', operationId: 'createOrder', outputs: { orderId: '$response.body#/id' } },
    {
      stepId: 'stock',
      workflowId: 'reserveStock',
      parameters: [{ name: 'orderId', value: '$steps.create.outputs.orderId' }],
      outputs: { reservationId: '$workflows.reserveStock.outputs.reservationId' },
    },
    {
      stepId: 'payment',
      workflowId: 'takePayment',
      parameters: [{ name: 'orderId', value: '$steps.create.outputs.orderId' }],
      outputs: { paymentId: '$workflows.takePayment.outputs.paymentId' },
    },
    {
      stepId: 'confirm',
      workflowId: 'confirmOrder',
      parameters: [
        { name: 'paymentId', value: '$steps.payment.outputs.paymentId' },
        { name: 'reservationId', value: '$steps.stock.outputs.reservationId' },
      ],
    },
    { stepId: 'status', operationId: 'getOrder', parameters: [{ name: 'id', in: 'path', value: '$steps.create.outputs.orderId' }] },
  ],
}

const document: ArazzoDocument = {
  arazzo: '1.1.0',
  workflows: [
    checkout,
    { workflowId: 'reserveStock', steps: [{ stepId: 'reserve' }], outputs: { reservationId: '$steps.reserve.outputs.id' } },
    { workflowId: 'takePayment', steps: [{ stepId: 'authorize' }], outputs: { paymentId: '$steps.authorize.outputs.id' } },
    { workflowId: 'confirmOrder', steps: [{ stepId: 'capture' }] },
  ],
}

describe('crossing arrows', () => {
  it('orders the Data flow view\'s rows so the checkout\'s arrows do not cross', () => {
    // Act
    const layout = layoutWorkflowGraph(buildDataFlowGraph(document, checkout))

    // Assert
    expect(crossings(layout)).toBe(0)
  })
})

describe('uncross', () => {
  it('leaves rows whose arrows do not cross as they are, even where another order would also do', () => {
    // Arrange
    const rows = [['a', 'b'], ['c', 'd']]

    // Act / Assert
    expect(uncross(rows, [['a', 'c'], ['b', 'd']])).toEqual(rows)
    expect(uncross([['a', 'b'], ['c']], [['a', 'c'], ['b', 'c']])).toEqual([['a', 'b'], ['c']])
  })

  it('moves boxes of two rows together when no one row can remove the crossing', () => {
    // Arrange: the checkout's rows as the walk numbers them; only swapping create/takePayment and payment/status
    // together uncrosses them.
    const rows = [['reserveStock', 'takePayment', 'create'], ['stock', 'payment', 'status'], ['confirm']]
    const links = [
      ['reserveStock', 'stock'],
      ['takePayment', 'payment'],
      ['create', 'stock'],
      ['create', 'payment'],
      ['create', 'status'],
      ['stock', 'confirm'],
      ['payment', 'confirm'],
    ] as const

    // Act
    const [top, middle] = uncross(rows, links)

    // Assert: the first order (in the given order's lexicographic terms) with no crossing.
    expect(top).toEqual(['reserveStock', 'create', 'takePayment'])
    expect(middle).toEqual(['stock', 'status', 'payment'])
  })

  it('falls back to sweeps for rows with too many orders to try, and never crosses more than given', () => {
    // Arrange: two rows of nine, wired in reverse, so every pair of arrows crosses.
    const upper = Array.from({ length: 9 }, (_, index) => `u${index}`)
    const lower = Array.from({ length: 9 }, (_, index) => `l${index}`)
    const links = upper.map((id, index) => [id, lower[8 - index]!] as const)

    // Act
    const [top, bottom] = uncross([upper, lower], links)

    // Assert
    const at = (row: string[], id: string) => row.indexOf(id)
    const crossed = links.filter(([a, b], i) => links.some(([c, d], j) => j > i && (at(top!, a) - at(top!, c)) * (at(bottom!, b) - at(bottom!, d)) < 0))
    expect(crossed).toHaveLength(0)
    expect(uncross([['x', 'y'], ['z']], [])).toEqual([['x', 'y'], ['z']])
  })
})
