// @vitest-environment happy-dom
import { mount, type VueWrapper } from '@vue/test-utils'
import { afterEach, describe, expect, it } from 'vitest'
import ArazzoWorkflow from '../src/components/ArazzoWorkflow.vue'
import type { ArazzoDocument, ArazzoWorkflow as Workflow } from '../src/types'

const pollStatus: Workflow = {
  workflowId: 'pollStatus',
  steps: [
    {
      stepId: 'status',
      operationId: 'getStatus',
      onFailure: [{ name: 'busy', type: 'retry', retryLimit: 3, retryAfter: 2, criteria: [{ condition: '$statusCode == 503' }] }],
    },
  ],
}
const document: ArazzoDocument = { arazzo: '1.1.0', workflows: [pollStatus] }

let wrapper: VueWrapper | undefined

afterEach(() => {
  wrapper?.unmount()
  wrapper = undefined
})

describe('the retry badge', () => {
  it('shows a step\'s retry of itself on its box, with the limit and delay, and its criteria in the tooltip', () => {
    // Arrange
    wrapper = mount(ArazzoWorkflow, {
      attachTo: globalThis.document.body,
      props: {
        view: { id: 'pollStatus', workflow: pollStatus, steps: pollStatus.steps!.map((step) => ({ id: `pollStatus/${step.stepId}`, step })) },
        document,
        documents: {},
      },
    })

    // Act
    const node = wrapper.find('.ba-overview [data-node-id="status"]')

    // Assert
    expect(node.find('.ba-diagram-retry text').text()).toBe('↻ ≤ 3× 2s')
    expect(node.find('title').text()).toContain('Retries itself: $statusCode == 503 — retry, ≤ 3×, after 2s (busy)')
  })
})
