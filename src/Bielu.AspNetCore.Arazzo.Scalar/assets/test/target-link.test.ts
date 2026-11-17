import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'
import { resolveStepTarget } from '../src/helpers/resolve-target'
import {
  documentSlugOf,
  installCrossDocumentHistory,
  navigationHref,
  openInOtherDocument,
  targetDisplayName,
  targetNavigationId,
} from '../src/helpers/target-link'
import type { ArazzoDocument, ArazzoStep } from '../src/types'

const ordersApi = {
  openapi: '3.1.0',
  'x-scalar-original-source-url': '/openapi/v1.json',
  paths: {
    '/orders': { post: { operationId: 'createOrder' } },
    '/orders/{id}': { get: {} },
  },
  // The sidebar tree Scalar's workspace store attaches to a loaded document.
  'x-scalar-navigation': {
    id: 'orders',
    type: 'document',
    children: [
      {
        id: 'orders/tag/orders',
        type: 'tag',
        children: [
          { id: 'orders/tag/orders/POST/orders', type: 'operation', method: 'post', path: '/orders' },
          { id: 'orders/tag/orders/GET/orders/{id}', type: 'operation', method: 'get', path: '/orders/{id}' },
        ],
      },
    ],
  },
}

const eventsApi = {
  asyncapi: '3.0.0',
  'x-scalar-original-source-url': '/asyncapi/v1.json',
  channels: { orderPlaced: { address: 'orders.placed' }, orderShipped: { address: 'orders.shipped' } },
  operations: { onOrderPlaced: { action: 'send', channel: { $ref: '#/channels/orderPlaced' } } },
  'x-scalar-navigation': {
    id: 'events',
    type: 'document',
    children: [
      {
        id: 'events/channel/orderplaced',
        type: 'asyncapi-channel',
        channelName: 'orderPlaced',
        children: [
          {
            id: 'events/channel/orderplaced/operation/onorderplaced',
            type: 'asyncapi-operation',
            operationName: 'onOrderPlaced',
            channelName: 'orderPlaced',
          },
        ],
      },
      { id: 'events/channel/ordershipped', type: 'asyncapi-channel', channelName: 'orderShipped' },
    ],
  },
}

const documents = { orders: ordersApi, events: eventsApi }

const arazzo: ArazzoDocument = {
  arazzo: '1.1.0',
  sourceDescriptions: [
    { name: 'orders', url: '/openapi/v1.json', type: 'openapi' },
    { name: 'events', url: '/asyncapi/v1.json', type: 'asyncapi' },
  ],
  workflows: [],
}

const targetOf = (step: Omit<ArazzoStep, 'stepId'>, loaded: Record<string, unknown> = documents) =>
  resolveStepTarget(arazzo, { stepId: 's', ...step }, loaded)

beforeEach(() => {
  vi.stubGlobal('location', { href: 'https://app.test/scalar/' })
})

afterEach(() => {
  vi.unstubAllGlobals()
})

describe('targetNavigationId', () => {
  it("finds an OpenAPI operation's sidebar entry by method and path", () => {
    // Arrange / Act / Assert
    expect(targetNavigationId(targetOf({ operationId: 'createOrder' }), documents)).toBe('orders/tag/orders/POST/orders')
    expect(
      targetNavigationId(targetOf({ operationPath: '{$sourceDescriptions.orders.url}#/paths/~1orders~1{id}/get' }), documents),
    ).toBe('orders/tag/orders/GET/orders/{id}')
  })

  it('opens an AsyncAPI operation target at the operation, and a channel target at the channel', () => {
    // Arrange / Act / Assert
    expect(targetNavigationId(targetOf({ operationId: 'onOrderPlaced' }), documents)).toBe(
      'events/channel/orderplaced/operation/onorderplaced',
    )
    expect(
      targetNavigationId(targetOf({ channelPath: '{$sourceDescriptions.events.url}#/channels/orderShipped', action: 'receive' }), documents),
    ).toBe('events/channel/ordershipped')
  })

  it('returns undefined for unresolved and workflow targets, or a document without a sidebar tree', () => {
    // Arrange
    const withoutNavigation = { orders: { ...ordersApi, 'x-scalar-navigation': undefined } }

    // Act / Assert
    expect(targetNavigationId(targetOf({ operationId: 'createOrder' }, {}), {})).toBeUndefined()
    expect(targetNavigationId(targetOf({ workflowId: 'other' }), documents)).toBeUndefined()
    expect(targetNavigationId(targetOf({ operationId: 'createOrder' }, withoutNavigation), withoutNavigation)).toBeUndefined()
    expect(targetNavigationId(undefined, documents)).toBeUndefined()
  })
})

describe('targetDisplayName', () => {
  it('names resolved targets instead of showing the raw reference', () => {
    // Arrange / Act / Assert
    expect(targetDisplayName(targetOf({ operationPath: '{$sourceDescriptions.orders.url}#/paths/~1orders/post' })!)).toBe(
      'createOrder',
    )
    expect(targetDisplayName(targetOf({ operationId: 'onOrderPlaced' })!)).toBe('onOrderPlaced')
    expect(targetDisplayName(targetOf({ channelPath: '{$sourceDescriptions.events.url}#/channels/orderShipped' })!)).toBe(
      'orderShipped',
    )
  })

  it('leaves an operation without an operationId to its signature, and an unresolved one as written', () => {
    // Arrange / Act / Assert
    expect(targetDisplayName(targetOf({ operationPath: '{$sourceDescriptions.orders.url}#/paths/~1orders~1{id}/get' })!)).toBe('')
    expect(targetDisplayName(targetOf({ operationId: 'missing' }, {})!)).toBe('missing')
  })
})

describe('cross-document history', () => {
  const fakeWindow = (hash: string) => {
    const listeners: (() => void)[] = []
    const page = {
      __bieluArazzoHistory: undefined as boolean | undefined,
      location: { hash, reload: vi.fn() },
      history: {
        pushState: vi.fn((_: unknown, __: string, url?: string | URL | null) => {
          page.location.hash = String(url ?? '').replace(/^[^#]*/, '')
        }),
        replaceState: vi.fn((_: unknown, __: string, url?: string | URL | null) => {
          page.location.hash = String(url ?? '').replace(/^[^#]*/, '')
        }),
      },
      addEventListener: vi.fn((_: string, listener: () => void) => listeners.push(listener)),
      back: (hash: string) => {
        page.location.hash = hash
        listeners.forEach((listener) => listener())
      },
    }
    return page
  }

  it('reads the document from the first hash segment', () => {
    // Arrange / Act / Assert
    expect(documentSlugOf('#events/channel/x')).toBe('events')
    expect(documentSlugOf('#my%20docs/x')).toBe('my docs')
    expect(documentSlugOf('#100%/x')).toBe('100%')
    expect(documentSlugOf('')).toBeUndefined()
  })

  it('reloads when Back lands in another document, and only then', () => {
    // Arrange
    const page = fakeWindow('#workflows/workflow/a')
    installCrossDocumentHistory(page as never)
    installCrossDocumentHistory(page as never)

    // Act: Scalar rewrites the URL while scrolling the same document, then Back within it.
    page.history.replaceState({}, '', '/scalar/#workflows/workflow/b')
    page.back('#workflows/workflow/a')

    // Assert
    expect(page.addEventListener).toHaveBeenCalledTimes(1)
    expect(page.location.reload).not.toHaveBeenCalled()

    // Act: Back into another document.
    page.back('#events/channel/x')

    // Assert
    expect(page.location.reload).toHaveBeenCalledTimes(1)
  })

  it('follows a document switch Scalar makes by rewriting the URL', () => {
    // Arrange
    const page = fakeWindow('#orders/tag/x')
    installCrossDocumentHistory(page as never)

    // Act: the document selector switches to Events (replaceState), then Back to an Events entry.
    page.history.replaceState({}, '', '/scalar/#events')
    page.back('#events/channel/y')

    // Assert
    expect(page.location.reload).not.toHaveBeenCalled()
  })
})

describe('navigationHref and openInOtherDocument', () => {
  it('keeps the page path and query and puts the entry id in the hash', () => {
    // Arrange / Act / Assert
    expect(navigationHref('events/channel/x', { pathname: '/scalar/', search: '?theme=moon' })).toBe(
      '/scalar/?theme=moon#events/channel/x',
    )
  })

  it('pushes the URL and reloads on a plain click, so Scalar picks the other document', () => {
    // Arrange
    const pushState = vi.fn()
    const reload = vi.fn()
    vi.stubGlobal('window', { history: { pushState, replaceState: vi.fn(), state: null }, location: { reload } })
    const event = { defaultPrevented: false, button: 0, preventDefault: vi.fn() } as unknown as MouseEvent

    // Act
    openInOtherDocument(event, '/scalar/#events/channel/x')

    // Assert
    expect(event.preventDefault).toHaveBeenCalled()
    expect(pushState).toHaveBeenCalledWith(null, '', '/scalar/#events/channel/x')
    expect(reload).toHaveBeenCalled()
  })

  it('leaves a modified or non-primary click to the browser', () => {
    // Arrange
    const reload = vi.fn()
    vi.stubGlobal('window', { history: { pushState: vi.fn() }, location: { reload } })
    const clicks = [{ ctrlKey: true }, { metaKey: true }, { shiftKey: true }, { button: 1 }, { defaultPrevented: true }]

    // Act
    for (const click of clicks) {
      openInOtherDocument({ button: 0, preventDefault: vi.fn(), ...click } as unknown as MouseEvent, '/x')
    }

    // Assert
    expect(reload).not.toHaveBeenCalled()
  })
})
