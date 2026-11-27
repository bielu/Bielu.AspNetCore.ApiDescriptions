// @vitest-environment happy-dom
import { describe, expect, it } from 'vitest'
import { plainText, renderMarkdown } from '../src/helpers/markdown'
import { isLikeC4Model, toViewSections, traverseLikeC4Model, viewsOf } from '../src/helpers/navigation'
import { STEP_LABEL_WIDTH, sequenceDiagram } from '../src/helpers/sequence'
import { drawOrder, dynamicSteps, edgeLabel, isSequence, labelArea, nodeLabel, stepNumber, viewBounds } from '../src/helpers/view-layout'
import handwrittenJson from './fixtures/handwritten.likec4.json'
import shopJson from './fixtures/shop.likec4.json'
import type { LikeC4Model, LikeC4Node, LikeC4View } from '../src/types'

/** The shop model `dotnet asyncapi likec4` generates from the AsyncAPI and Arazzo fixtures, exported by LikeC4 1.61. */
const shop = shopJson as unknown as LikeC4Model
/** A hand-written model with nested elements, exported by LikeC4 1.61. */
const handwritten = handwrittenJson as unknown as LikeC4Model

const node = (overrides: Partial<LikeC4Node>): LikeC4Node => ({ id: 'n', x: 0, y: 0, width: 320, height: 180, ...overrides })

describe('isLikeC4Model', () => {
  it('claims what likec4 export json writes', () => {
    // Act & Assert
    expect(isLikeC4Model(shop as unknown as Record<string, unknown>)).toBe(true)
    expect(isLikeC4Model(handwritten as unknown as Record<string, unknown>)).toBe(true)
  })

  it('claims a model that is not laid out, so it gets a note instead of an empty OpenAPI page', () => {
    // Act & Assert
    expect(isLikeC4Model({ _stage: 'computed', elements: {}, views: {} })).toBe(true)
  })

  it('leaves OpenAPI, AsyncAPI and Arazzo documents alone', () => {
    // Act & Assert
    expect(isLikeC4Model({ openapi: '3.1.0', paths: {} })).toBe(false)
    expect(isLikeC4Model({ asyncapi: '3.0.0', channels: {} })).toBe(false)
    expect(isLikeC4Model({ arazzo: '1.0.1', workflows: [] })).toBe(false)
    expect(isLikeC4Model({ _stage: 'layouted', elements: [], views: {} })).toBe(false)
  })
})

describe('traverseLikeC4Model', () => {
  it('lists one entry per view, the index view first, with ids prefixed by the document', () => {
    // Act
    const entries = traverseLikeC4Model({ documentId: 'arch', documentName: 'arch', document: shop as unknown as Record<string, unknown> } as never)

    // Assert
    expect(entries.map((entry) => entry.id)).toEqual([
      'arch/view/index',
      'arch/view/asyncapiChannels',
      'arch/view/asyncapiServices',
      'arch/view/asyncapiOrders',
      'arch/view/asyncapiInventory',
      'arch/view/asyncapiNotifications',
      'arch/view/arazzoPlaceOrder',
      'arch/view/arazzoNotifyShopper',
    ])
    expect(entries[1]).toEqual({ id: 'arch/view/asyncapiChannels', title: 'AsyncAPI: services and channels', kind: 'view' })
  })

  it('moves the index view to the front, and titles a view without a title by its id', () => {
    // Arrange
    const document = { _stage: 'layouted', elements: {}, views: { first: { title: '' }, index: { id: 'index', title: 'Landscape' }, broken: 'x' } }

    // Act
    const entries = traverseLikeC4Model({ documentId: 'd', document })

    // Assert
    expect(entries.map((entry) => entry.title)).toEqual(['Landscape', 'first'])
  })

  it('lists nothing for a document without views', () => {
    // Act & Assert
    expect(viewsOf(undefined)).toEqual([])
    expect(traverseLikeC4Model({ documentId: 'd', document: {} })).toEqual([])
  })
})

describe('toViewSections', () => {
  it('pairs each view with the entry Scalar hands back', () => {
    // Arrange
    const navigation = traverseLikeC4Model({ documentId: 'x', document: handwritten as unknown as Record<string, unknown> })

    // Act
    const sections = toViewSections(handwritten, navigation)

    // Assert
    expect(sections).toHaveLength(1)
    expect(sections[0]!.id).toBe('x/view/index')
    expect(sections[0]!.view.title).toBe('Shop (hand-written)')
    expect(toViewSections(handwritten, [])).toEqual([])
  })
})

describe('nodeLabel', () => {
  it('centres the title, technology and description in the node', () => {
    // Act
    const lines = nodeLabel(node({ title: 'Orders created', technology: 'kafka', description: { md: 'An order was **placed**.' } }))

    // Assert
    expect(lines.map((line) => [line.part, line.text])).toEqual([
      ['title', 'Orders created'],
      ['technology', '[kafka]'],
      ['description', 'An order was placed.'],
    ])
    expect(lines[0]!.y).toBeGreaterThan(50)
    expect(lines[2]!.y).toBeLessThan(130)
  })

  it('leaves out a description a small node has no room for', () => {
    // Act
    const lines = nodeLabel(node({ height: 60, title: 'Tiny', description: 'Never shown' }))

    // Assert
    expect(lines.map((line) => line.part)).toEqual(['title'])
  })

  it('puts a container title, upper-cased, at its top-left', () => {
    // Act
    const lines = nodeLabel(node({ title: 'Shop', children: ['shop.orders'], labelBBox: { x: 6, y: 0, width: 35, height: 15 } }))

    // Assert
    expect(lines).toEqual([{ text: 'SHOP', y: 18, part: 'title' }])
  })

  it('keeps a queue label clear of its ends', () => {
    // Act
    const area = labelArea(node({ shape: 'queue' }))

    // Assert
    expect(area.center).toBeLessThan(160)
    expect(area.width).toBeLessThan(288)
    expect(labelArea(node({}))).toEqual({ center: 160, width: 288 })
  })
})

describe('edgeLabel', () => {
  it('numbers a dynamic view step and adds the technology', () => {
    // Act
    const label = edgeLabel(
      { id: 'step-02', source: 'a', target: 'b', label: 'sends OrderCreated', technology: 'kafka', labelBBox: { x: 10, y: 20, width: 140, height: 20 } },
      true,
    )

    // Assert
    expect(label?.lines.map((line) => line.text)).toEqual(['2. sends OrderCreated', '[kafka]'])
    expect(label?.box.height).toBeGreaterThanOrEqual(33)
  })

  it('draws no label without a box or text', () => {
    // Act & Assert
    expect(edgeLabel({ id: 'e', source: 'a', target: 'b', label: 'x' }, false)).toBeUndefined()
    expect(edgeLabel({ id: 'e', source: 'a', target: 'b', labelBBox: { x: 0, y: 0, width: 1, height: 1 } }, false)).toBeUndefined()
  })
})

describe('stepNumber', () => {
  it('reads the step number from a dynamic view edge id', () => {
    // Act & Assert
    expect(stepNumber('step-01')).toBe('1')
    expect(stepNumber('step-02.01')).toBe('2.1')
    expect(stepNumber('w6ae47')).toBeUndefined()
  })
})

describe('drawOrder', () => {
  it('draws containers first, outermost first, then the elements', () => {
    // Act
    const order = drawOrder([
      node({ id: 'leaf', level: 2 }),
      node({ id: 'inner', level: 1, children: ['leaf'] }),
      node({ id: 'outer', level: 0, children: ['inner'] }),
    ])

    // Assert
    expect(order.map((entry) => entry.id)).toEqual(['outer', 'inner', 'leaf'])
  })
})

describe('viewBounds', () => {
  it('uses LikeC4 bounds, else what the nodes cover', () => {
    // Act & Assert
    expect(viewBounds(shop.views!.index!)).toEqual(shop.views!.index!.bounds)
    expect(viewBounds({ id: 'v', nodes: [node({ x: 10, y: 20, width: 30, height: 40 }), node({ x: 50, y: 0, width: 10, height: 10 })] })).toEqual({
      x: 10,
      y: 0,
      width: 50,
      height: 60,
    })
    expect(viewBounds({ id: 'empty' })).toEqual({ x: 0, y: 0, width: 1, height: 1 })
  })

  it('uses the sequence layout bounds only for a view drawn as a sequence', () => {
    // Arrange
    const placeOrder = shop.views!.arazzoPlaceOrder as LikeC4View
    const asDiagram = { ...placeOrder, variant: 'diagram' as const, bounds: { x: 0, y: 0, width: 900, height: 700 } }

    // Act & Assert
    expect(viewBounds(placeOrder)).toEqual(placeOrder.sequenceLayout!.bounds)
    expect(viewBounds(asDiagram)).toEqual(asDiagram.bounds)
  })
})

describe('sequenceDiagram', () => {
  const placeOrder = shop.views!.arazzoPlaceOrder as LikeC4View

  it('draws a dynamic view laid out as a sequence as one, and other views as diagrams', () => {
    // Act & Assert
    expect(isSequence(placeOrder)).toBe(true)
    expect(isSequence(shop.views!.asyncapiChannels!)).toBe(false)
    expect(isSequence({ ...placeOrder, variant: 'diagram' })).toBe(false)
  })

  it('places a lane per actor and a step between their ports, in order', () => {
    // Act
    const diagram = sequenceDiagram(placeOrder)!

    // Assert
    expect(diagram.lanes.map((lane) => lane.id)).toEqual(['shopper', 'payments', 'ordersCreated', 'inventoryReserved', 'inventoryStockChanged', 'warehouse'])
    expect(diagram.steps.map((step) => step.number)).toEqual(['1', '2', '3', '4', '5'])
    const pay = diagram.steps[0]!
    // The shopper's port sits on its lifeline (x 160) at y -4 + 254; the arrow starts at the activation bar's edge.
    expect(pay.points).toEqual([
      [165, 250],
      [602, 250],
    ])
    expect(pay.label.anchor).toBe('middle')
  })

  it('draws a step back to its own lane as a loop, labelled beside it', () => {
    // Act
    const notify = sequenceDiagram(placeOrder)!.steps[4]!

    // Assert
    expect(notify.self).toBe(true)
    expect(notify.points).toHaveLength(4)
    expect(notify.label.anchor).toBe('start')
    expect(notify.edge.navigateTo).toBe('arazzoNotifyShopper')
  })

  it('points an arrow left when the target lane is left of the source', () => {
    // Act
    const delivered = sequenceDiagram(placeOrder)!.steps[2]!

    // Assert
    expect(delivered.points[0]![0]).toBeGreaterThan(delivered.points[1]![0])
  })

  it('leaves out steps whose ports are missing, and draws frames LikeC4 adds', () => {
    // Arrange
    const view: LikeC4View = {
      ...placeOrder,
      sequenceLayout: {
        ...placeOrder.sequenceLayout,
        steps: [{ id: 'step-01', sourceHandle: 'nowhere', targetHandle: 'step-01_target' }],
        compounds: [{ id: 'c', origin: 'shop', x: 0, y: 0, width: 10, height: 10, depth: 1 }],
        parallelAreas: [{ parallelPrefix: 'step-02.', x: 0, y: 0, width: 5, height: 5 }],
      },
    }

    // Act
    const diagram = sequenceDiagram(view)!

    // Assert
    expect(diagram.steps).toEqual([])
    expect(diagram.frames.map((frame) => [frame.id, frame.kind])).toEqual([
      ['shop', 'compound'],
      ['step-02.', 'parallel'],
    ])
  })

  it('runs lifelines to the view bottom, else past the lanes, when the layout has no bounds', () => {
    // Arrange
    const actor = { id: 'a', x: 0, y: 0, width: 100, height: 180, ports: [{ id: 'p', cx: 50, cy: 300, height: 20 }] }
    const view: LikeC4View = { id: 'v', _type: 'dynamic', variant: 'sequence', sequenceLayout: { actors: [actor] } }

    // Act
    const withViewBounds = sequenceDiagram({ ...view, bounds: { x: 0, y: 0, width: 100, height: 600 } })!
    const withoutBounds = sequenceDiagram(view)!

    // Assert
    expect(withViewBounds.lanes[0]!.lifeline).toEqual({ x: 50, top: 180, bottom: 600 })
    expect(withoutBounds.lanes[0]!.lifeline).toEqual({ x: 50, top: 180, bottom: 310 })
  })

  it('covers a self-step loop and its label when the layout has no bounds', () => {
    // Arrange: a port at the right edge of its lane, and a step back to the same lane.
    const actor = {
      id: 'a',
      x: 0,
      y: 0,
      width: 100,
      height: 180,
      ports: [
        { id: 'out', cx: 100, cy: 220 },
        { id: 'in', cx: 100, cy: 260 },
      ],
    }
    const view: LikeC4View = {
      id: 'v',
      _type: 'dynamic',
      variant: 'sequence',
      edges: [{ id: 'step-01', source: 'a', target: 'a' }],
      sequenceLayout: { actors: [actor], steps: [{ id: 'step-01', sourceHandle: 'out', targetHandle: 'in' }] },
    }

    // Act
    const diagram = sequenceDiagram(view)!

    // Assert
    const step = diagram.steps[0]!
    const right = diagram.bounds.x + diagram.bounds.width
    expect(Math.max(...step.points.map((point) => point[0]))).toBeLessThanOrEqual(right)
    expect(step.label.x + STEP_LABEL_WIDTH).toBeLessThanOrEqual(right)
    expect(diagram.lanes[0]!.lifeline.bottom).toBe(272)
  })

  it('draws nothing without a sequence layout', () => {
    // Act & Assert
    expect(sequenceDiagram(shop.views!.index!)).toBeUndefined()
  })
})

describe('dynamicSteps', () => {
  it('lists a dynamic view steps with their numbers, and none for other views', () => {
    // Act & Assert
    expect(dynamicSteps(shop.views!.arazzoPlaceOrder!).map((step) => step.number)).toEqual(['1', '2', '3', '4', '5'])
    expect(dynamicSteps(shop.views!.index!)).toEqual([])
  })
})

describe('markdown', () => {
  it('renders Markdown sanitized, and plain text escaped', () => {
    // Act & Assert
    expect(renderMarkdown({ md: '**bold**' })).toContain('<strong>bold</strong>')
    expect(renderMarkdown({ txt: '<b>not bold</b>\nnext' })).toBe('&lt;b&gt;not bold&lt;/b&gt;<br>next')
    expect(renderMarkdown(undefined)).toBe('')
  })

  it('flattens a text to one plain line for a label or tooltip', () => {
    // Act & Assert
    expect(plainText({ md: 'See [the docs](https://x) for **more**\n\nnext' })).toBe('See the docs for more next')
    expect(plainText('plain')).toBe('plain')
    expect(plainText({ txt: 'C# uses  order_id > 0\n*now*' })).toBe('C# uses order_id > 0 *now*')
    expect(plainText(null)).toBe('')
  })
})
