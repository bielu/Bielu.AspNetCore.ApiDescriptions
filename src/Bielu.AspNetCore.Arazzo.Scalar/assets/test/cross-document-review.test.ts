// @vitest-environment happy-dom
import { flushPromises, mount, type VueWrapper } from '@vue/test-utils'
import { afterEach, describe, expect, it, vi } from 'vitest'
import ArazzoDocument from '../src/components/ArazzoDocument.vue'
import { traverseArazzoDocument } from '../src/helpers/navigation'
import { resolveStepTarget, resolveWorkflow, sourcesLoaded } from '../src/helpers/resolve-target'
import { summarizeSources, targetSpec } from '../src/helpers/target-spec'
import type { ArazzoDocument as Document } from '../src/types'

/** Orders → Identity → MFA: each calls a workflow of the next, published as an Arazzo document of its own. */
const orders = {
  arazzo: '1.1.0',
  info: { title: 'Orders', version: '1.0.0' },
  'x-scalar-original-source-url': 'https://shop.test/orders.arazzo.json',
  sourceDescriptions: [{ name: 'identity', url: '/identity.arazzo.json', type: 'arazzo' }],
  workflows: [{ workflowId: 'placeOrder', steps: [{ stepId: 'login', workflowId: '$sourceDescriptions.identity.signIn' }] }],
} as unknown as Document

const identity = {
  arazzo: '1.0.1',
  'x-scalar-original-source-url': 'https://shop.test/identity.arazzo.json',
  sourceDescriptions: [{ name: 'mfa', url: '/mfa.arazzo.json', type: 'arazzo' }],
  workflows: [{ workflowId: 'signIn', steps: [{ stepId: 'verify', workflowId: '$sourceDescriptions.mfa.challenge' }] }],
} as unknown as Document

const mfa = {
  arazzo: '1.0.1',
  'x-scalar-original-source-url': 'https://shop.test/mfa.arazzo.json',
  workflows: [{ workflowId: 'challenge', steps: [{ stepId: 'sendCode' }] }],
} as unknown as Document

let wrapper: VueWrapper | undefined

afterEach(() => {
  wrapper?.unmount()
  wrapper = undefined
  vi.useRealTimers()
})

describe('Arazzo documents that load after the overview', () => {
  it('knows which sources are still missing, through every Arazzo document reached', () => {
    // Act / Assert
    expect(sourcesLoaded(orders, { orders })).toBe(false)
    expect(sourcesLoaded(orders, { orders, identity })).toBe(false)
    expect(sourcesLoaded(orders, { orders, identity, mfa })).toBe(true)
  })

  it('keeps watching until they arrive, so their workflows become expandable without a reload', async () => {
    // Arrange: Scalar fills one workspace object in place, Orders first.
    vi.useFakeTimers()
    const documents: Record<string, unknown> = { orders }
    wrapper = mount(ArazzoDocument, {
      attachTo: globalThis.document.body,
      props: {
        document: orders,
        documentName: 'orders',
        documents,
        navigation: traverseArazzoDocument({ documentId: 'orders', document: orders as unknown as Record<string, unknown> }),
      },
    })
    await flushPromises()
    const toggle = (node: string) => wrapper!.find(`.ba-overview [data-node-id="${node}"] .ba-diagram-toggle[role="button"]`)
    expect(toggle('login').exists()).toBe(false)

    // Act: Identity loads on a later tick.
    documents.identity = identity
    await vi.advanceTimersByTimeAsync(1500)
    await flushPromises()

    // Assert
    expect(toggle('login').exists()).toBe(true)

    // Act: expand it; MFA, reached through Identity, loads later still.
    await toggle('login').trigger('click')
    await flushPromises()
    expect(toggle('login/verify').exists()).toBe(false)
    documents.mfa = mfa
    await vi.advanceTimersByTimeAsync(5000)
    await flushPromises()

    // Assert
    expect(toggle('login/verify').exists()).toBe(true)
  })
})

describe('the base URI of a workflow source', () => {
  const caller = (self?: string) =>
    ({
      arazzo: '1.1.0',
      'x-scalar-original-source-url': 'https://api.test/root.json',
      ...(self ? { $self: self } : {}),
      sourceDescriptions: [{ name: 'other', url: 'other.json', type: 'arazzo' }],
      workflows: [],
    }) as unknown as Document
  const callee = (url: string) => ({ arazzo: '1.1.0', 'x-scalar-original-source-url': url, workflows: [{ workflowId: 'w', steps: [{ stepId: 's' }] }] })

  it('is the authored $self, not the URL the document was retrieved from', () => {
    // Act / Assert
    const canonical = { other: callee('https://canonical.test/workflows/other.json') }
    expect(resolveWorkflow(caller('https://canonical.test/workflows/root.json'), '$sourceDescriptions.other.w', canonical)?.documentName).toBe('other')
    expect(resolveWorkflow(caller(), '$sourceDescriptions.other.w', canonical)).toBeUndefined()
  })

  it('resolves a relative $self against the retrieval URL', () => {
    // Act / Assert
    const sibling = { other: callee('https://api.test/workflows/other.json') }
    expect(resolveWorkflow(caller('/workflows/root.json'), '$sourceDescriptions.other.w', sibling)?.documentName).toBe('other')
  })

  it('falls back to the retrieval URL without $self', () => {
    // Act / Assert
    expect(resolveWorkflow(caller(), '$sourceDescriptions.other.w', { other: callee('https://api.test/other.json') })?.documentName).toBe('other')
  })
})

describe('a workflow called in another document', () => {
  it('is shown with that document\'s Arazzo version, and counted under its source', () => {
    // Arrange
    const documents = { orders, identity }
    const step = orders.workflows![0]!.steps![0]!

    // Act
    const spec = targetSpec(orders, step, resolveStepTarget(orders, step, documents), documents)
    const sources = summarizeSources(orders, documents)

    // Assert
    expect(spec).toMatchObject({ family: 'arazzo', version: '1.0.1', resolved: true, sourceName: 'identity' })
    expect(sources.find((summary) => summary.source.name === 'identity')).toMatchObject({ stepCount: 1 })
  })
})
