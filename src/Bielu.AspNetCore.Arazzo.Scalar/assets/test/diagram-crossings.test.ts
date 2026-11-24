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

  it('orders rows the way recounting every arrow for each swap did, for diagrams too big to search', () => {
    // Arrange: seeded random diagrams of three to five rows of six to nine boxes (too many orders to try them all),
    // some arrows skipping a row.
    let seed = 42
    const random = () => (seed = (seed * 1_103_515_245 + 12_345) % 2_147_483_648) / 2_147_483_648
    let reordered = 0
    for (let diagram = 0; diagram < 40; diagram++) {
      const rows = Array.from({ length: 3 + Math.floor(random() * 3) }, (_, row) =>
        Array.from({ length: 6 + Math.floor(random() * 4) }, (_, at) => `r${row}b${at}`),
      )
      const links: [string, string][] = []
      rows.forEach((row, index) =>
        row.forEach((id) => {
          for (let skip = 1; skip <= 2 && index + skip < rows.length; skip++) {
            const below = rows[index + skip]!
            for (let count = Math.floor(random() * (skip === 1 ? 3 : 2)); count > 0; count--) {
              links.push([id, below[Math.floor(random() * below.length)]!])
            }
          }
        }),
      )

      // Act
      const result = uncross(rows, links)

      // Assert
      expect(result).toEqual(recountingUncross(rows, links))
      reordered += Number(JSON.stringify(result) !== JSON.stringify(rows))
    }
    // The diagrams exercise the sweeps: most of them are reordered.
    expect(reordered).toBeGreaterThan(20)
  })

  it('bounds the time spent on two fully connected rows, where no order crosses fewer arrows', () => {
    // Arrange: 35 boxes over 35, every box joined to every box of the other row (1225 arrows); every order crosses
    // the same number of them. Recounting every arrow for each attempted swap took about ten seconds here.
    const upper = Array.from({ length: 35 }, (_, index) => `u${index}`)
    const lower = Array.from({ length: 35 }, (_, index) => `l${index}`)
    const links = upper.flatMap((u) => lower.map((l) => [u, l] as const))

    // Act
    const started = performance.now()
    const result = uncross([upper, lower], links)
    const elapsed = performance.now() - started

    // Assert: the given order, quickly.
    expect(result).toEqual([upper, lower])
    expect(elapsed).toBeLessThan(1_000)
  })
})

/**
 * The barycenter sweeps as they were before swaps were scored by the arrows they affect: every attempted swap
 * recounts every crossing, and all eight passes run. A reference for the result the faster version must keep.
 */
const recountingUncross = (rows: string[][], links: readonly (readonly [string, string])[]): string[][] => {
  const count = (current: string[][]) => {
    const place = new Map<string, [number, number]>()
    current.forEach((row, index) => row.forEach((id, at) => place.set(id, [index, at])))
    const spans = links.flatMap(([v, w]) => {
      const [a, b] = [place.get(v), place.get(w)]
      if (!a || !b || a[0] === b[0]) {
        return []
      }
      const [upper, lower] = a[0] < b[0] ? [a, b] : [b, a]
      return [{ rows: `${upper[0]}:${lower[0]}`, from: upper[1], to: lower[1] }]
    })
    return spans.reduce((sum, a, i) => sum + spans.slice(i + 1).filter((b) => a.rows === b.rows && (a.from - b.from) * (a.to - b.to) < 0).length, 0)
  }
  const neighbours = new Map<string, string[]>()
  for (const [v, w] of links) {
    neighbours.set(v, [...(neighbours.get(v) ?? []), w])
    neighbours.set(w, [...(neighbours.get(w) ?? []), v])
  }
  const current = rows.map((row) => [...row])
  let best = rows.map((row) => [...row])
  let fewest = count(current)
  const sweep = (index: number, beside: string[]) => {
    const position = new Map(beside.map((id, at) => [id, at]))
    const row = current[index]!
    const weight = new Map(
      row.map((id, at) => {
        const linked = (neighbours.get(id) ?? []).flatMap((other) => (position.has(other) ? [position.get(other)!] : []))
        return [id, linked.length ? linked.reduce((sum, value) => sum + value, 0) / linked.length : (at * beside.length) / row.length]
      }),
    )
    current[index] = row
      .map((id, at) => ({ id, at }))
      .sort((a, b) => weight.get(a.id)! - weight.get(b.id)! || a.at - b.at)
      .map(({ id }) => id)
  }
  for (let pass = 0; pass < 8 && fewest > 0; pass++) {
    if (pass % 2 === 0) {
      for (let index = 1; index < current.length; index++) sweep(index, current[index - 1]!)
    } else {
      for (let index = current.length - 2; index >= 0; index--) sweep(index, current[index + 1]!)
    }
    for (let improved = true; improved; ) {
      improved = false
      for (const row of current) {
        for (let at = 0; at + 1 < row.length; at++) {
          const before = count(current)
          ;[row[at], row[at + 1]] = [row[at + 1]!, row[at]!]
          if (count(current) < before) {
            improved = true
          } else {
            ;[row[at], row[at + 1]] = [row[at + 1]!, row[at]!]
          }
        }
      }
    }
    if (count(current) < fewest) {
      fewest = count(current)
      best = current.map((row) => [...row])
    }
  }
  return best
}
