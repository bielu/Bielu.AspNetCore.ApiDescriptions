// @vitest-environment happy-dom
import { flushPromises, mount } from '@vue/test-utils'
import { afterEach, describe, expect, it, vi } from 'vitest'
import ArazzoDiagramExport from '../src/components/ArazzoDiagramExport.vue'
import ArazzoWorkflowDiagram from '../src/components/ArazzoWorkflowDiagram.vue'
import { safeName, standaloneSvg, toMermaid } from '../src/helpers/diagram-export'
import { buildWorkflowGraph, layoutWorkflowGraph } from '../src/helpers/workflow-graph'
import type { ArazzoDocument, ArazzoWorkflow } from '../src/types'

const placeOrder: ArazzoWorkflow = {
  workflowId: 'placeOrder',
  steps: [
    {
      stepId: 'create',
      successCriteria: [{ condition: '$statusCode == 201' }],
      onFailure: [{ name: 'rejected', type: 'end', criteria: [{ condition: '$statusCode == 400' }] }],
    },
    { stepId: 'pay', onFailure: [{ name: 'declined', type: 'end', criteria: [{ condition: '$message.payload.status == "Declined"' }] }] },
  ],
}

const trackOrder: ArazzoWorkflow = { workflowId: 'trackOrder', steps: [{ stepId: 'place', workflowId: 'placeOrder' }, { stepId: 'status' }] }

const document: ArazzoDocument = { arazzo: '1.1.0', workflows: [placeOrder, trackOrder] }

afterEach(() => {
  vi.restoreAllMocks()
  vi.unstubAllGlobals()
})

describe('toMermaid', () => {
  it('writes a top-down flowchart with the boxes, labelled arrows and coloured ends', () => {
    // Arrange
    const layout = layoutWorkflowGraph(buildWorkflowGraph(document, placeOrder))

    // Act
    const mermaid = toMermaid(layout)

    // Assert
    expect(mermaid).toBe(
      [
        'flowchart TD',
        '  n_create["create"]',
        '  n_pay["pay"]',
        '  n_success(["Success"])',
        '  n_end_name_rejected(["rejected"])',
        '  n_end_name_declined(["declined"])',
        '  n_create --> n_pay',
        '  n_create -.->|"$statusCode == 400"| n_end_name_rejected',
        '  n_pay -.->|"$message.payload.status == #quot;Declined#quot;"| n_end_name_declined',
        '  n_pay --> n_success',
        '  classDef success fill:#e6f4ea,stroke:#30a46c,color:#1b5e20',
        '  classDef failure fill:#fdecea,stroke:#e5484d,color:#8b1a1a',
        '  class n_success success',
        '  class n_end_name_rejected,n_end_name_declined failure',
        '',
      ].join('\n'),
    )
  })

  it('draws an expanded workflow as a subgraph, with its node as a subroutine', () => {
    // Arrange
    const layout = layoutWorkflowGraph(buildWorkflowGraph(document, trackOrder, undefined, { expanded: new Set(['place']) }))

    // Act
    const mermaid = toMermaid(layout)

    // Assert
    expect(mermaid).toContain('  n_place[["place"]]')
    expect(mermaid).toMatch(/ {2}subgraph n_place_group\["placeOrder"\]\n {4}n_place_create\["create"\]/)
    expect(mermaid).toContain('  n_place_pay --> n_place_success')
    expect(mermaid).toContain('  n_place -.- n_place_create')
  })

  it('groups the Pages view steps of each page band in a subgraph titled with the page and route', () => {
    // Arrange
    const orders = {
      workflowId: 'orders',
      steps: [
        { stepId: 'listOrders', 'x-page': 'orderList', 'x-label': 'Load the orders' },
        { stepId: 'listFilters', 'x-page': 'orderList' },
        { stepId: 'audit' },
      ],
    } as unknown as ArazzoWorkflow
    const pagesDocument = { arazzo: '1.1.0', 'x-pages': { orderList: { title: 'Order list', route: '/orders' } }, workflows: [orders] } as ArazzoDocument
    const graph = buildWorkflowGraph(pagesDocument, orders, undefined, {
      view: 'pages',
      pages: { pagesExtension: 'x-pages', pageExtension: 'x-page', labelExtension: 'x-label' },
    })

    // Act
    const mermaid = toMermaid(layoutWorkflowGraph(graph))

    // Assert
    expect(mermaid).toMatch(/ {2}subgraph n_band_0\["Order list · \/orders"\]\n {4}n_listOrders\["Load the orders"\]\n {4}n_listFilters\["listFilters"\]\n {2}end/)
    expect(mermaid).toMatch(/ {2}subgraph n_band_1\["No page"\]\n {4}n_audit\["audit"\]\n {2}end/)
  })

  it('keeps ids unique when two keys sanitise alike', () => {
    // Arrange
    const layout = layoutWorkflowGraph(
      buildWorkflowGraph({ arazzo: '1.1.0', workflows: [{ workflowId: 'w', steps: [{ stepId: 'a.b' }, { stepId: 'a_b' }] }] }, {
        workflowId: 'w',
        steps: [{ stepId: 'a.b' }, { stepId: 'a_b' }],
      }),
    )

    // Act
    const mermaid = toMermaid(layout)

    // Assert
    expect(mermaid).toContain('n_a_b["a.b"]')
    expect(mermaid).toContain('n_a_b_2["a_b"]')
  })
})

describe('safeName', () => {
  it('keeps letters, digits, dashes and underscores', () => {
    // Act / Assert
    expect(safeName('orders.placed/{id}')).toBe('orders_placed_id_')
    expect(safeName('')).toBe('workflow')
  })
})

describe('standaloneSvg', () => {
  it('copies the rendered overview at its natural size, with its styles written onto it and its classes dropped', () => {
    // Arrange
    const layout = layoutWorkflowGraph(buildWorkflowGraph(document, placeOrder))
    const view = mount(ArazzoWorkflowDiagram, { attachTo: globalThis.document.body, props: { layout, stepSectionIds: {}, workflowId: 'placeOrder' } })
    const svg = view.find('svg').element as SVGSVGElement

    // Act
    const exported = standaloneSvg(svg, layout)

    // Assert
    const parsed = new DOMParser().parseFromString(exported, 'image/svg+xml').documentElement
    expect(exported.startsWith('<?xml')).toBe(true)
    expect(parsed.getAttribute('width')).toBe(String(layout.width))
    expect(parsed.getAttribute('viewBox')).toBe(`0 0 ${layout.width} ${layout.height}`)
    expect(parsed.querySelector('[class]')).toBeNull()
    expect(parsed.querySelector('[tabindex]')).toBeNull()
    expect(exported).toContain('>create<')
    expect(svg.querySelector('[class]')).not.toBeNull()
    view.unmount()
  })
})

describe('the export menu', () => {
  const layout = layoutWorkflowGraph(buildWorkflowGraph(document, placeOrder))

  const mountMenu = () => {
    const diagram = mount(ArazzoWorkflowDiagram, { attachTo: globalThis.document.body, props: { layout, stepSectionIds: {}, workflowId: 'placeOrder' } })
    const menu = mount(ArazzoDiagramExport, {
      attachTo: globalThis.document.body,
      props: { layout, name: 'place.order', svg: () => diagram.find('svg').element as SVGSVGElement },
    })
    return { diagram, menu }
  }

  /** Captures the files the menu saves, by the link it clicks. */
  const captureDownloads = () => {
    const saved: { name: string; blob: Blob }[] = []
    const blobs = new Map<string, Blob>()
    vi.spyOn(URL, 'createObjectURL').mockImplementation((blob) => {
      const url = `blob:${blobs.size}`
      blobs.set(url, blob as Blob)
      return url
    })
    vi.spyOn(URL, 'revokeObjectURL').mockImplementation(() => {})
    vi.spyOn(HTMLAnchorElement.prototype, 'click').mockImplementation(function (this: HTMLAnchorElement) {
      saved.push({ name: this.download, blob: blobs.get(this.href)! })
    })
    return saved
  }

  it('downloads the overview as an SVG file named after the workflow', async () => {
    // Arrange
    const saved = captureDownloads()
    const { diagram, menu } = mountMenu()

    // Act
    await menu.findAll('button').find((button) => button.text() === 'Download SVG')!.trigger('click')

    // Assert
    expect(saved.map((file) => file.name)).toEqual(['place_order-overview.svg'])
    expect(saved[0]!.blob.type).toBe('image/svg+xml')
    expect(await saved[0]!.blob.text()).toContain('>pay<')
    expect(menu.find('.ba-export-status').text()).toBe('SVG downloaded')
    menu.unmount()
    diagram.unmount()
  })

  it('copies Mermaid source, and downloads it when there is no clipboard', async () => {
    // Arrange
    const writeText = vi.fn().mockResolvedValue(undefined)
    vi.stubGlobal('navigator', { clipboard: { writeText } })
    const saved = captureDownloads()
    const { diagram, menu } = mountMenu()
    const copy = () => menu.findAll('button').find((button) => button.text() === 'Copy as Mermaid')!.trigger('click')

    // Act
    await copy()
    await flushPromises()

    // Assert
    expect(writeText).toHaveBeenCalledWith(toMermaid(layout))
    expect(menu.find('.ba-export-status').text()).toBe('Mermaid copied')

    // Act: the clipboard refuses.
    writeText.mockRejectedValue(new Error('denied'))
    await copy()
    await flushPromises()

    // Assert
    expect(saved.map((file) => file.name)).toEqual(['place_order-overview.mmd'])
    expect(menu.find('.ba-export-status').text()).toBe('Mermaid downloaded')
    menu.unmount()
    diagram.unmount()
  })

  it('says when the PNG cannot be made', async () => {
    // Arrange: an image that never loads.
    captureDownloads()
    vi.stubGlobal(
      'Image',
      class {
        onerror?: () => void
        set src(_value: string) {
          setTimeout(() => this.onerror?.())
        }
      },
    )
    const { diagram, menu } = mountMenu()

    // Act
    await menu.findAll('button').find((button) => button.text() === 'Download PNG')!.trigger('click')
    await new Promise((resolve) => setTimeout(resolve, 5))
    await flushPromises()

    // Assert
    expect(menu.find('.ba-export-status').text()).toBe('The PNG could not be made; try SVG')
    menu.unmount()
    diagram.unmount()
  })
})
