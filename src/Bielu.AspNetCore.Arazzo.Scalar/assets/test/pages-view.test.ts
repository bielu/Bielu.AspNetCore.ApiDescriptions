import { describe, expect, it } from 'vitest'
import { pagesInUse, type PagesConfig } from '../src/helpers/pages'
import { pluginOptionsFromModuleUrl, resolvePluginOptions } from '../src/helpers/plugin-options'
import {
  buildWorkflowGraph,
  endNodeId,
  layoutWorkflowGraph,
  SUCCESS_NODE_ID,
  workflowNodeId,
} from '../src/helpers/workflow-graph'
import type { ArazzoDocument, ArazzoFailureAction, ArazzoWorkflow } from '../src/types'

/** A shop: browsing orders needs a signed-in customer; failures end on pages of their own. */
const config: PagesConfig = { pagesExtension: 'x-shop-pages', pageExtension: 'x-page', labelExtension: 'x-label' }

const failures = (): ArazzoFailureAction[] => [
  { name: 'signIn', type: 'retry', workflowId: 'signIn', retryLimit: 1, criteria: [{ condition: '$statusCode == 401' }], 'x-label': 'Not signed in' } as ArazzoFailureAction,
  { name: 'showForbiddenPage', type: 'end', criteria: [{ condition: '$statusCode == 403' }], 'x-page': 'forbidden', 'x-label': 'No permission' } as ArazzoFailureAction,
  { name: 'showErrorPage', type: 'end', 'x-page': 'error' } as ArazzoFailureAction,
]

const signIn: ArazzoWorkflow = {
  workflowId: 'signIn',
  summary: 'Sign the customer in',
  steps: [
    { stepId: 'authorize', 'x-page': 'signIn', 'x-label': 'Open the sign-in page' } as never,
    { stepId: 'exchangeCode', 'x-page': 'signIn' } as never,
  ],
}

const browseOrders: ArazzoWorkflow = {
  workflowId: 'browseOrders',
  steps: [
    { stepId: 'loadAccount', workflowId: 'loadAccount' },
    { stepId: 'listOrders', 'x-page': 'orders', 'x-label': 'Load the order list', onFailure: failures() } as never,
    { stepId: 'listFilters', 'x-page': 'orders' } as never,
    { stepId: 'audit' },
  ],
}

const loadAccount: ArazzoWorkflow = {
  workflowId: 'loadAccount',
  'x-label': 'Load the customer account',
  steps: [{ stepId: 'getAccount', onFailure: failures() }],
} as ArazzoWorkflow

const document = {
  arazzo: '1.1.0',
  'x-shop-pages': {
    orders: { title: 'Order list', route: '/orders', description: 'All orders of the customer.' },
    signIn: { title: 'Sign in', route: '/sign-in' },
    forbidden: { title: 'No permission', route: '/forbidden' },
    error: { title: 'Something went wrong', route: '/error' },
  },
  workflows: [browseOrders, loadAccount, signIn],
} as ArazzoDocument

const pages = (workflow: ArazzoWorkflow, expanded: ReadonlySet<string> = new Set()) =>
  buildWorkflowGraph(document, workflow, undefined, { view: 'pages', pages: config, expanded })

describe('pagesInUse', () => {
  it('is true only when the options are configured and the document uses them', () => {
    // Act / Assert
    expect(pagesInUse(document, config)).toBe(true)
    expect(pagesInUse(document, {})).toBe(false)
    expect(pagesInUse({ arazzo: '1.1.0', workflows: [{ workflowId: 'wf', steps: [{ stepId: 'a' }] }] }, config)).toBe(false)
  })

  it('counts a label alone as use', () => {
    // Act / Assert
    expect(pagesInUse({ arazzo: '1.1.0', workflows: [{ workflowId: 'wf', steps: [{ stepId: 'a', 'x-label': 'Do it' } as never] }] }, { labelExtension: 'x-label' })).toBe(true)
  })
})

describe('the Pages view of the graph', () => {
  it('labels steps in plain language, keeping the step id for the tooltip', () => {
    // Act
    const graph = pages(browseOrders)

    // Assert
    expect(graph.nodes.find((node) => node.id === 'listOrders')).toMatchObject({ label: 'Load the order list', technical: 'listOrders', page: 'orders' })
    expect(graph.nodes.find((node) => node.id === 'audit')).toMatchObject({ label: 'audit', technical: 'audit' })
  })

  it('groups consecutive steps on the same page into one band, and steps without a page into a "No page" band', () => {
    // Act
    const graph = pages(browseOrders)

    // Assert
    expect(graph.bands.map((band) => ({ title: band.title, route: band.route, nodes: band.nodes, page: band.pageId }))).toEqual([
      { title: 'No page', route: undefined, nodes: ['loadAccount'], page: undefined },
      { title: 'Order list', route: '/orders', nodes: ['listOrders', 'listFilters'], page: 'orders' },
      { title: 'No page', route: undefined, nodes: ['audit'], page: undefined },
    ])
  })

  it('gives the steps of an expanded workflow bands of their own inside its group', () => {
    // Arrange
    const signInNode = workflowNodeId('', 'signIn')

    // Act
    const graph = pages(browseOrders, new Set([signInNode]))

    // Assert
    expect(graph.bands).toContainEqual(
      expect.objectContaining({ title: 'Sign in', route: '/sign-in', nodes: [`${signInNode}/authorize`, `${signInNode}/exchangeCode`], group: `${signInNode}::group` }),
    )
  })

  it('labels actions in plain language, falling back to their criteria or "otherwise"', () => {
    // Act
    const graph = pages(browseOrders)

    // Assert
    const from = graph.edges.filter((edge) => edge.from === 'listOrders')
    expect(from.find((edge) => edge.action === 'signIn')!.label).toBe('Not signed in — retry, ≤ 1×')
    expect(from.find((edge) => edge.action === 'showForbiddenPage')!.label).toBe('No permission')
    expect(from.find((edge) => edge.action === 'showErrorPage')!.label).toBe('otherwise')
  })

  it('labels the arrow to Success after the last step "done"', () => {
    // Act
    const graph = pages(browseOrders)

    // Assert
    expect(graph.edges.find((edge) => edge.from === 'audit' && edge.to === SUCCESS_NODE_ID)!.label).toBe('done')
  })

  it('names end nodes after the page they show, with the action\'s label under it', () => {
    // Act
    const graph = pages(browseOrders)

    // Assert
    expect(graph.nodes.find((node) => node.id === endNodeId('', 'name:showForbiddenPage'))).toMatchObject({ label: 'No permission', subtitle: 'No permission', page: 'forbidden' })
    expect(graph.nodes.find((node) => node.id === endNodeId('', 'name:showErrorPage'))).toMatchObject({ label: 'Something went wrong', page: 'error' })
  })

  it('names the Success node after the page a success end action lands on, with a check mark', () => {
    // Arrange
    const workflow: ArazzoWorkflow = {
      workflowId: 'checkout',
      steps: [{ stepId: 'pay', onSuccess: [{ name: 'paid', type: 'end', 'x-page': 'orders' } as never] }],
    }

    // Act
    const graph = buildWorkflowGraph({ ...document, workflows: [workflow] }, workflow, undefined, { view: 'pages', pages: config })

    // Assert
    expect(graph.nodes.find((node) => node.id === SUCCESS_NODE_ID)!.label).toBe('Order list ✓')
  })

  it('names workflow nodes after the workflow they call', () => {
    // Act
    const graph = pages(browseOrders)

    // Assert
    expect(graph.nodes.find((node) => node.id === 'loadAccount')).toMatchObject({ label: 'Load the customer account', technical: 'loadAccount' })
    expect(graph.nodes.find((node) => node.id === workflowNodeId('', 'signIn'))).toMatchObject({ label: 'Sign the customer in', technical: 'signIn' })
  })

  it('keeps the node ids and the order of the Technical view', () => {
    // Act
    const technical = buildWorkflowGraph(document, loadAccount, undefined, { pages: config })
    const inPages = pages(loadAccount)
    const xs = (graph: typeof technical) => {
      const layout = layoutWorkflowGraph(graph)
      return [...layout.nodes].sort((a, b) => a.y - b.y || a.x - b.x).map((node) => node.id)
    }

    // Assert
    expect(inPages.nodes.map((node) => node.id).sort()).toEqual(technical.nodes.map((node) => node.id).sort())
    expect(xs(inPages)).toEqual(xs(technical))
    expect(technical.bands).toEqual([])
  })

  it('never draws a band over a box that is not one of its steps', () => {
    // Arrange: two steps on the same page, the second beside the first one's end nodes.
    const workflow: ArazzoWorkflow = {
      workflowId: 'track',
      steps: [
        { stepId: 'check', 'x-page': 'orders', onFailure: failures(), onSuccess: [{ name: 'next', type: 'goto', stepId: 'wait' }] } as never,
        { stepId: 'wait', 'x-page': 'orders' } as never,
      ],
    }
    const signInNode = workflowNodeId('', 'signIn')

    // Act
    const layouts = [
      layoutWorkflowGraph(buildWorkflowGraph({ ...document, workflows: [workflow, signIn] } as ArazzoDocument, workflow, undefined, { view: 'pages', pages: config })),
      layoutWorkflowGraph(pages(browseOrders, new Set(['loadAccount', `loadAccount/${signInNode}`]))),
    ]

    // Assert
    for (const layout of layouts) {
      for (const band of layout.bands) {
        const covered = layout.nodes.filter(
          (node) =>
            !band.nodes.includes(node.id) &&
            Math.abs(node.x - band.x) < (node.width + band.width) / 2 &&
            Math.abs(node.y - band.y) < (node.height + band.height) / 2,
        )
        expect(covered.map((node) => node.id), band.title).toEqual([])
      }
    }
  })

  it('leaves room for a band\'s title under an expanded workflow\'s label', () => {
    // Act
    const layout = layoutWorkflowGraph(pages(browseOrders, new Set(['loadAccount'])))

    // Assert
    const group = layout.groups.find((candidate) => candidate.id === 'loadAccount::group')!
    const band = layout.bands.find((candidate) => candidate.group === 'loadAccount::group')!
    expect(band.y - band.height / 2).toBeGreaterThanOrEqual(group.y - group.height / 2 + 20)
  })

  it('lays every band out around its steps', () => {
    // Act
    const layout = layoutWorkflowGraph(pages(browseOrders, new Set(['loadAccount'])))

    // Assert
    expect(layout.bands.length).toBeGreaterThan(0)
    for (const band of layout.bands) {
      for (const id of band.nodes) {
        const node = layout.nodes.find((candidate) => candidate.id === id)!
        expect(node.x - node.width / 2).toBeGreaterThanOrEqual(band.x - band.width / 2)
        expect(node.x + node.width / 2).toBeLessThanOrEqual(band.x + band.width / 2)
        expect(node.y - node.height / 2).toBeGreaterThanOrEqual(band.y - band.height / 2)
        expect(node.y + node.height / 2).toBeLessThanOrEqual(band.y + band.height / 2)
      }
    }
  })
})

describe('pages options', () => {
  it('reads the pages extensions from every source, accepting only specification extensions', () => {
    // Act / Assert
    expect(resolvePluginOptions({ pagesExtension: 'x-shop-pages', pageExtension: 'x-page', labelExtension: 'x-label' })).toEqual(config)
    expect(resolvePluginOptions(undefined, { pagesExtension: 'shop-pages', labelExtension: 'x-label' })).toEqual({ labelExtension: 'x-label' })
    expect(pluginOptionsFromModuleUrl('https://cdn.test/p.mjs?pagesExtension=x-shop-pages&pageExtension=x-page&labelExtension=x-label&endLabelExtension=x-page'))
      .toEqual({ ...config, endLabelExtension: 'x-page' })
  })
})
