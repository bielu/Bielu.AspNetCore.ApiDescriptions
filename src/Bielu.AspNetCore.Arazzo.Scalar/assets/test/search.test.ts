import { describe, expect, it } from 'vitest'
import type { WorkflowView } from '../src/helpers/navigation'
import { filterWorkflows, searchTerms } from '../src/helpers/search'

const views: WorkflowView[] = [
  {
    id: 'd/workflow/placeOrder',
    workflow: { workflowId: 'placeOrder', summary: 'Place an order and announce it' },
    steps: [
      { id: 'd/workflow/placeOrder/step/create', step: { stepId: 'create', operationId: 'createOrder' } },
      {
        id: 'd/workflow/placeOrder/step/announce',
        step: { stepId: 'announce', channelPath: '{$sourceDescriptions.events.url}#/channels/orderPlaced', action: 'send' },
      },
    ],
  },
  {
    id: 'd/workflow/trackOrder',
    workflow: { workflowId: 'trackOrder', summary: 'Follow an order to shipment' },
    steps: [
      { id: 'd/workflow/trackOrder/step/status', step: { stepId: 'status', description: 'Check the order status.' } },
      { id: 'd/workflow/trackOrder/step/shipped', step: { stepId: 'shipped', action: 'receive' } },
    ],
  },
]

describe('filterWorkflows', () => {
  it('shows everything for an empty or blank query', () => {
    // Arrange / Act / Assert
    expect(filterWorkflows(views, '')).toBeUndefined()
    expect(filterWorkflows(views, '   ')).toBeUndefined()
  })

  it('keeps every step of a workflow that matches by itself', () => {
    // Arrange / Act
    const filter = filterWorkflows(views, 'shipment')!

    // Assert
    expect([...filter.workflows]).toEqual(['d/workflow/trackOrder'])
    expect(filter.matchingSteps).toBe(2)
    expect(filter.totalSteps).toBe(4)
  })

  it('finds steps by id, target and action, case-insensitively', () => {
    // Arrange / Act / Assert
    expect([...filterWorkflows(views, 'CREATEORDER')!.steps]).toEqual(['d/workflow/placeOrder/step/create'])
    expect([...filterWorkflows(views, 'orderplaced send')!.steps]).toEqual(['d/workflow/placeOrder/step/announce'])
    expect([...filterWorkflows(views, 'receive')!.steps]).toEqual(['d/workflow/trackOrder/step/shipped'])
  })

  it('combines a workflow term with a step term', () => {
    // Arrange / Act
    const filter = filterWorkflows(views, 'trackOrder status')!

    // Assert
    expect([...filter.workflows]).toEqual(['d/workflow/trackOrder'])
    expect([...filter.steps]).toEqual(['d/workflow/trackOrder/step/status'])
  })

  it('finds nothing when a term is missing everywhere', () => {
    // Arrange / Act
    const filter = filterWorkflows(views, 'create nonexistent')!

    // Assert
    expect(filter.workflows.size).toBe(0)
    expect(filter.matchingSteps).toBe(0)
  })

  it('splits the query on any whitespace', () => {
    // Arrange / Act / Assert
    expect(searchTerms('  Order\tSEND \n')).toEqual(['order', 'send'])
  })
})
