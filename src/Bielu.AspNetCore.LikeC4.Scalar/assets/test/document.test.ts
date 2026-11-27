// @vitest-environment happy-dom
import { flushPromises, mount, type VueWrapper } from '@vue/test-utils'
import { afterEach, describe, expect, it, vi } from 'vitest'
import LikeC4Document from '../src/components/LikeC4Document.vue'
import { traverseLikeC4Model } from '../src/helpers/navigation'
import handwrittenJson from './fixtures/handwritten.likec4.json'
import shopJson from './fixtures/shop.likec4.json'
import type { LikeC4Model } from '../src/types'

const shop = shopJson as unknown as LikeC4Model
const handwritten = handwrittenJson as unknown as LikeC4Model

let wrapper: VueWrapper | undefined
afterEach(() => {
  wrapper?.unmount()
  wrapper = undefined
})

const render = async (document: LikeC4Model, extra: Record<string, unknown> = {}) => {
  wrapper = mount(LikeC4Document, {
    attachTo: globalThis.document.body,
    props: {
      document,
      documentName: 'architecture',
      navigation: traverseLikeC4Model({ documentId: 'architecture', document: document as unknown as Record<string, unknown> }),
      ...extra,
    },
  })
  await flushPromises()
  return wrapper
}

describe('LikeC4Document', () => {
  it('draws a section per view, with the id of its sidebar entry', async () => {
    // Act
    const view = await render(shop)

    // Assert
    const sections = view.findAll('section.bl-view')
    expect(sections.map((section) => section.attributes('id'))).toEqual(
      traverseLikeC4Model({ documentId: 'architecture', document: shop as unknown as Record<string, unknown> }).map((entry) => entry.id),
    )
    expect(view.find('.bl-kicker').text()).toContain('8 views · 11 elements')
    // The project is LikeC4's unnamed `default`, so the source's name titles the model.
    expect(view.find('.bl-title').text()).toBe('architecture')
  })

  it('draws every node and edge of an element view where LikeC4 laid it out', async () => {
    // Act
    const view = await render(shop)

    // Assert
    const channels = view.find('svg[data-view-id="asyncapiChannels"]')
    const layout = shop.views!.asyncapiChannels!
    expect(channels.findAll('.bl-node')).toHaveLength(layout.nodes!.length)
    expect(channels.findAll('.bl-edge')).toHaveLength(layout.edges!.length)
    const orders = layout.nodes!.find((node) => node.id === 'orders')!
    expect(channels.find('[data-node-id="orders"]').attributes('transform')).toBe(`translate(${orders.x},${orders.y})`)
    expect(channels.find('[data-node-id="orders"] .bl-node-title').text()).toBe('Orders API')
    expect(channels.attributes('viewBox')).toBe(`-16 -16 ${layout.bounds!.width + 32} ${layout.bounds!.height + 32}`)
  })

  it('names each diagram after its view, as a group that keeps its links', async () => {
    // Act
    const view = await render(shop)

    // Assert
    const channels = view.find('svg[data-view-id="asyncapiChannels"]')
    expect(channels.attributes('role')).toBe('group')
    expect(channels.attributes('aria-label')).toBe(shop.views!.asyncapiChannels!.title)
    const sequence = view.find('svg.bl-sequence[data-view-id="arazzoPlaceOrder"]')
    expect(sequence.attributes('role')).toBe('group')
    expect(sequence.attributes('aria-label')).toBe(shop.views!.arazzoPlaceOrder!.title)
  })

  it('names a diagram after its view id when its title is blank', async () => {
    // Arrange
    const model = structuredClone(shop)
    model.views!.asyncapiChannels!.title = '   '
    model.views!.arazzoPlaceOrder!.title = ''

    // Act
    const view = await render(model)

    // Assert
    expect(view.find('svg[data-view-id="asyncapiChannels"]').attributes('aria-label')).toBe('asyncapiChannels')
    expect(view.find('svg.bl-sequence[data-view-id="arazzoPlaceOrder"]').attributes('aria-label')).toBe('arazzoPlaceOrder')
  })

  it('draws the tail arrowhead of a sequence step, as well as its head', async () => {
    // Arrange: the first step points both ways.
    const model = structuredClone(shop)
    const placeOrder = model.views!.arazzoPlaceOrder!
    placeOrder.edges = placeOrder.edges!.map((edge, index) => (index === 0 ? { ...edge, head: 'none', tail: 'normal' } : edge))

    // Act
    const view = await render(model)

    // Assert
    const step = view.find('svg.bl-sequence [data-edge-id="' + placeOrder.edges[0]!.id + '"]')
    const arrows = step.findAll('.bl-edge-arrow')
    expect(arrows).toHaveLength(1)
    // Its tip is the step's source end: the first point of its line.
    const [source] = step.find('.bl-edge-line').attributes('points')!.split(' ')
    const [x, y] = source!.split(',').map(Number)
    expect(arrows[0]!.attributes('d')).toMatch(new RegExp('^M' + Math.round(x! * 10) / 10 + ',' + Math.round(y! * 10) / 10 + ' '))
    const other = view.find('svg.bl-sequence [data-edge-id="' + placeOrder.edges[1]!.id + '"]')
    expect(other.findAll('.bl-edge-arrow')).toHaveLength(1)
  })

  it('draws sends solid and deliveries dashed, as the generated specification says', async () => {
    // Act
    const view = await render(shop)

    // Assert
    const lineOf = (label: string) =>
      view
        .findAll('svg[data-view-id="asyncapiChannels"] .bl-edge')
        .find((edge) => edge.find('title').text().startsWith(label))!
        .find('.bl-edge-line')
        .attributes('stroke-dasharray')
    expect(lineOf('sends OrderCreated')).toBeUndefined()
    expect(lineOf('delivers OrderCreated')).toBe('6 5')
  })

  it('draws a sequence view as lanes and numbered steps, and lists the steps with their notes', async () => {
    // Act
    const view = await render(shop)

    // Assert
    const sequence = view.find('svg.bl-sequence[data-view-id="arazzoPlaceOrder"]')
    expect(sequence.findAll('.bl-lifeline')).toHaveLength(6)
    expect(sequence.findAll('.bl-step').map((step) => step.find('text').text())).toEqual([
      '1. createPayment',
      '2. sends OrderCreated',
      '3. delivers InventoryReserved',
      '4. delivers StockLevelChanged',
      '5. runs Notify the shopper',
    ])
    const steps = view.find('[data-nav-id="architecture/view/arazzoPlaceOrder"] .bl-steps')
    expect(steps.findAll('li')).toHaveLength(5)
    expect(steps.find('li').text()).toContain('Charges the shopper')
  })

  it('opens the view a step leads to, from the diagram and from the step list', async () => {
    // Arrange
    const emit = vi.fn()
    const view = await render(shop, { eventBus: { emit } })
    const scrolled = vi.fn()
    globalThis.document.getElementById('architecture/view/arazzoNotifyShopper')!.scrollIntoView = scrolled

    // Act
    await view.find('svg.bl-sequence [data-edge-id="step-05"]').trigger('click')
    await view.find('.bl-link-button').trigger('click')

    // Assert
    expect(scrolled).toHaveBeenCalledTimes(2)
    expect(emit).toHaveBeenCalledWith('intersecting:nav-item', { id: 'architecture/view/arazzoNotifyShopper' })
    expect(view.find('svg.bl-sequence [data-edge-id="step-05"]').attributes('role')).toBe('link')
    expect(view.find('svg.bl-sequence [data-edge-id="step-01"]').attributes('role')).toBeUndefined()
  })

  it('draws nested elements inside their containers', async () => {
    // Act
    const view = await render(handwritten)

    // Assert
    const containers = view.findAll('.bl-container')
    expect(containers.map((container) => container.attributes('data-node-id'))).toEqual(['shop', 'shop.orders'])
    expect(view.find('.bl-container .bl-node-title').text()).toBe('SHOP')
    expect(view.find('[data-node-id="shop.orders.api"] .bl-node-title').text()).toBe('Orders API')
  })

  it('switches a view between fitted and actual size', async () => {
    // Arrange
    const view = await render(handwritten)
    const button = view.find('.bl-canvas-toolbar button')

    // Act
    await button.trigger('click')

    // Assert
    expect(view.find('.bl-canvas').classes()).not.toContain('bl-canvas-fitted')
    expect(button.text()).toBe('Fit to width')
    expect(button.attributes('aria-pressed')).toBe('true')
  })

  it('says how to export a model that is not laid out', async () => {
    // Act
    const view = await render({ ...handwritten, _stage: 'computed' })

    // Assert
    expect(view.find('.bl-note').text()).toContain('likec4 export json')
    expect(view.findAll('section.bl-view')).toHaveLength(0)
  })

  it('says when a model has no views, and titles it by its named project', async () => {
    // Act
    const view = await render({ _stage: 'layouted', project: { id: 'shop', title: 'Web shop' }, elements: {}, views: {} })

    // Assert
    expect(view.text()).toContain('This model has no views.')
    expect(view.find('.bl-title').text()).toBe('Web shop')
  })
})
