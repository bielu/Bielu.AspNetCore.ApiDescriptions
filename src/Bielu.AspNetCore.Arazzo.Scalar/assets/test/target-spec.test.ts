import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'
import { resolveStepTarget, sourceNameOf } from '../src/helpers/resolve-target'
import { countStepsBySpec, documentSpec, specLabel, summarizeSources, targetSpec } from '../src/helpers/target-spec'
import { buildWorkflowGraph, layoutWorkflowGraph, SPEC_MARKER_WIDTH } from '../src/helpers/workflow-graph'
import type { ArazzoDocument, ArazzoStep } from '../src/types'

const ordersApi = {
  openapi: '3.1.0',
  info: { title: 'Orders' },
  'x-scalar-original-source-url': '/openapi/v1.json',
  paths: { '/orders': { post: { operationId: 'createOrder' } } },
}

const eventsApi = {
  asyncapi: '3.0.0',
  info: { title: 'Events' },
  'x-scalar-original-source-url': '/asyncapi/v1.json',
  channels: { orderCreated: { address: 'orders.created' } },
  operations: { onOrderCreated: { action: 'receive', channel: { $ref: '#/channels/orderCreated' } } },
}

const steps: ArazzoStep[] = [
  { stepId: 'create', operationId: 'createOrder' },
  { stepId: 'created', channelPath: '{$sourceDescriptions.eventsApi.url}#/channels/orderCreated', action: 'receive' },
  { stepId: 'listen', operationId: 'onOrderCreated' },
  { stepId: 'again', workflowId: 'placeOrder' },
]

const arazzo: ArazzoDocument = {
  arazzo: '1.1.0',
  sourceDescriptions: [
    { name: 'ordersApi', url: '/openapi/v1.json', type: 'openapi' },
    { name: 'eventsApi', url: '/asyncapi/v1.json', type: 'asyncapi' },
  ],
  workflows: [{ workflowId: 'placeOrder', steps }],
}

const loaded = { orders: ordersApi, events: eventsApi }

const specOf = (step: ArazzoStep, documents: Record<string, unknown> = loaded, document: ArazzoDocument = arazzo) =>
  targetSpec(document, step, resolveStepTarget(document, step, documents), documents)

beforeEach(() => {
  vi.stubGlobal('location', { href: 'https://app.test/scalar/' })
})

afterEach(() => {
  vi.unstubAllGlobals()
})

describe('targetSpec', () => {
  it('takes the spec and version of the loaded document an OpenAPI operation resolves in', () => {
    // Arrange / Act
    const spec = specOf(steps[0]!)

    // Assert
    expect(spec).toEqual({ family: 'openapi', version: '3.1.0', resolved: true, sourceName: 'ordersApi' })
  })

  it('tells an AsyncAPI operationId apart from an OpenAPI one', () => {
    // Arrange / Act
    const spec = specOf(steps[2]!)

    // Assert
    expect(spec).toEqual({ family: 'asyncapi', version: '3.0.0', resolved: true, sourceName: 'eventsApi' })
  })

  it('marks a step that invokes a workflow as Arazzo', () => {
    // Arrange / Act
    const spec = specOf(steps[3]!)

    // Assert
    expect(spec).toEqual({ family: 'arazzo', version: '1.1.0', resolved: true, sourceName: undefined })
  })

  it('falls back to the type of the source the step names when the document is not loaded', () => {
    // Arrange / Act
    const spec = specOf(steps[1]!, {})

    // Assert
    expect(spec).toEqual({ family: 'asyncapi', version: undefined, resolved: false, sourceName: 'eventsApi' })
  })

  it('reads the spec from the pointer when the named source declares no type', () => {
    // Arrange
    const document: ArazzoDocument = { arazzo: '1.0.1', sourceDescriptions: [{ name: 'api', url: '/x.json' }], workflows: [] }
    const step: ArazzoStep = { stepId: 's', operationPath: '{$sourceDescriptions.api.url}#/paths/~1orders/post' }

    // Act
    const spec = specOf(step, {}, document)

    // Assert
    expect(spec).toMatchObject({ family: 'openapi', resolved: false, sourceName: 'api' })
  })

  it('uses the one spec all sources share for a bare operationId that is not loaded', () => {
    // Arrange
    const document: ArazzoDocument = {
      arazzo: '1.0.1',
      sourceDescriptions: [
        { name: 'a', url: '/a.json', type: 'openapi' },
        { name: 'b', url: '/b.json', type: 'openapi' },
      ],
      workflows: [],
    }

    // Act
    const spec = specOf({ stepId: 's', operationId: 'missing' }, {}, document)

    // Assert
    expect(spec).toEqual({ family: 'openapi', version: undefined, resolved: false, sourceName: undefined })
  })

  it('returns undefined for a bare operationId that is not loaded when the sources mix specs', () => {
    // Arrange / Act / Assert
    expect(specOf({ stepId: 's', operationId: 'missing' }, {})).toBeUndefined()
  })

  it('returns undefined for a step without a target', () => {
    // Arrange / Act / Assert
    expect(specOf({ stepId: 's' })).toBeUndefined()
  })
})

describe('sourceNameOf', () => {
  it('reads the source from a pointer or a qualified operationId, and none from a bare one', () => {
    // Arrange / Act / Assert
    expect(sourceNameOf('{$sourceDescriptions.eventsApi.url}#/channels/x')).toBe('eventsApi')
    expect(sourceNameOf('$sourceDescriptions.ordersApi.createOrder')).toBe('ordersApi')
    expect(sourceNameOf('createOrder')).toBeUndefined()
  })
})

describe('documentSpec and specLabel', () => {
  it('recognizes OpenAPI, Swagger and AsyncAPI documents', () => {
    // Arrange / Act / Assert
    expect(documentSpec(ordersApi)).toEqual({ family: 'openapi', version: '3.1.0' })
    expect(documentSpec({ swagger: '2.0' })).toEqual({ family: 'openapi', version: '2.0' })
    expect(documentSpec(eventsApi)).toEqual({ family: 'asyncapi', version: '3.0.0' })
    expect(documentSpec({ arazzo: '1.0.1' })).toBeUndefined()
    expect(documentSpec(undefined)).toBeUndefined()
  })

  it("reports the version the author wrote, which Scalar keeps when it upgrades a document", () => {
    // Arrange
    const upgradedAsyncApi = { asyncapi: '3.1.0', 'x-original-aas-version': '3.0.0' }
    const upgradedSwagger = { openapi: '3.1.1', 'x-original-oas-version': '2.0' }

    // Act / Assert
    expect(documentSpec(upgradedAsyncApi)).toEqual({ family: 'asyncapi', version: '3.0.0' })
    expect(documentSpec(upgradedSwagger)).toEqual({ family: 'openapi', version: '2.0' })
    expect(documentSpec({ asyncapi: '3.1.0', 'x-original-aas-version': '' })).toEqual({ family: 'asyncapi', version: '3.1.0' })
  })

  it('names the spec, with the version when known', () => {
    // Arrange / Act / Assert
    expect(specLabel({ family: 'asyncapi', version: '3.0.0' })).toBe('AsyncAPI 3.0.0')
    expect(specLabel({ family: 'openapi', version: undefined })).toBe('OpenAPI')
  })
})

describe('summarizeSources', () => {
  it('reports each source as loaded, with its version and the steps that use it', () => {
    // Arrange / Act
    const summaries = summarizeSources(arazzo, loaded)

    // Assert
    expect(summaries.map(({ source, ...rest }) => ({ name: source.name, ...rest }))).toEqual([
      { name: 'ordersApi', family: 'openapi', version: '3.1.0', documentName: 'orders', stepCount: 1 },
      { name: 'eventsApi', family: 'asyncapi', version: '3.0.0', documentName: 'events', stepCount: 2 },
    ])
  })

  it('falls back to the declared type for a source that is not loaded', () => {
    // Arrange / Act
    const summaries = summarizeSources(arazzo, {})

    // Assert
    expect(summaries.map((summary) => [summary.source.name, summary.family, summary.documentName, summary.stepCount])).toEqual([
      ['ordersApi', 'openapi', undefined, 0],
      ['eventsApi', 'asyncapi', undefined, 1],
    ])
  })
})

describe('countStepsBySpec', () => {
  it('counts the steps per spec, in a fixed order', () => {
    // Arrange / Act / Assert
    expect(countStepsBySpec(arazzo, loaded)).toEqual([
      { family: 'openapi', count: 1 },
      { family: 'asyncapi', count: 2 },
      { family: 'arazzo', count: 1 },
    ])
  })
})

describe('workflow graph spec markers', () => {
  it('marks each step node with its spec and makes room for the marker', () => {
    // Arrange
    const workflow = arazzo.workflows![0]!
    const plain = layoutWorkflowGraph(buildWorkflowGraph(arazzo, workflow))

    // Act
    const marked = layoutWorkflowGraph(buildWorkflowGraph(arazzo, workflow, (step) => specOf(step)?.family))

    // Assert
    // The steps, not the Success node the last step leads to.
    expect(marked.nodes.filter((node) => node.stepId).map((node) => node.spec)).toEqual(['openapi', 'asyncapi', 'asyncapi', 'arazzo'])
    expect(plain.nodes.every((node) => node.spec === undefined)).toBe(true)
    expect(marked.nodes[0]!.width).toBe(plain.nodes[0]!.width + SPEC_MARKER_WIDTH)
  })
})
