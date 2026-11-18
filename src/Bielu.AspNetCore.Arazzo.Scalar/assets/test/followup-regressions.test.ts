// @vitest-environment happy-dom
import { mount } from '@vue/test-utils'
import { expect, it } from 'vitest'
import { createArazzoPlugin } from '../src/plugin'
import { buildWorkflowGraph } from '../src/helpers/workflow-graph'
import type { Component } from 'vue'
import type { ArazzoDocument } from '../src/types'

it('preserves the terminal failure path after an unconditional bounded retry', () => {
  const document: ArazzoDocument = {
    arazzo: '1.1.0',
    workflows: [{
      workflowId: 'pay',
      steps: [{
        stepId: 'charge',
        onFailure: [
          { name: 'again', type: 'retry', retryLimit: 1 },
          { name: 'rejected', type: 'end' },
          { name: 'unreachable', type: 'end' },
        ],
      }],
    }],
  }
  const graph = buildWorkflowGraph(document, document.workflows![0])
  expect(graph.nodes.find((node) => node.stepId === 'charge')?.retries).toHaveLength(1)
  expect(graph.edges.some((edge) => edge.action === 'rejected')).toBe(true)
  expect(graph.edges.some((edge) => edge.action === 'unreachable')).toBe(false)
})

it('keeps configuration on each rendered reference across factory creation and remounts', () => {
  if (!customElements.get('bielu-arazzo-document')) {
    customElements.define('bielu-arazzo-document', class extends HTMLElement {
      arazzoOptions = {}
    })
  }
  const first = createArazzoPlugin({ pageExtension: 'x-first' })
  const second = createArazzoPlugin({ pageExtension: 'x-second' })
  for (const factory of [first, second, second, first]) {
    const component = factory().documentTypes[0]!.content.component as Component
    const view = mount(component, { attrs: { documentName: 'orders' } })
    const element = view.element as HTMLElement & { arazzoOptions: unknown }
    expect(element.arazzoOptions).toEqual({ pageExtension: factory === first ? 'x-first' : 'x-second' })
    expect(element.getAttribute('documentname')).toBe('orders')
    view.unmount()
  }
})

