import { describe, expect, it } from 'vitest'
import { criterionTypeLabel, formatDuration } from '../src/helpers/format'
import { resolveReusables } from '../src/helpers/reusable'
import type { ArazzoDocument, ArazzoFailureAction, ArazzoParameter } from '../src/types'

describe('criterionTypeLabel', () => {
  it('shows no badge for a simple condition, written out or not', () => {
    // Arrange / Act / Assert
    expect(criterionTypeLabel(undefined)).toBeUndefined()
    expect(criterionTypeLabel('simple')).toBeUndefined()
    expect(criterionTypeLabel({ type: 'simple' })).toBeUndefined()
  })

  it('names the expression language, with the version of a Criterion Expression Type Object', () => {
    // Arrange / Act / Assert
    expect(criterionTypeLabel('regex')).toBe('regex')
    expect(criterionTypeLabel({ type: 'jsonpath', version: 'rfc9535' })).toBe('jsonpath rfc9535')
    expect(criterionTypeLabel({ type: 'xpath' })).toBe('xpath')
  })

  it('shows no badge for an expression type object without a type', () => {
    // Arrange / Act / Assert
    expect(criterionTypeLabel({ version: '1.0' })).toBeUndefined()
  })
})

describe('formatDuration', () => {
  it('keeps short timeouts in milliseconds', () => {
    // Arrange / Act / Assert
    expect(formatDuration(0)).toBe('0 ms')
    expect(formatDuration(500)).toBe('500 ms')
  })

  it('uses seconds, then minutes', () => {
    // Arrange / Act / Assert
    expect(formatDuration(30_000)).toBe('30 s')
    expect(formatDuration(1_500)).toBe('1.5 s')
    expect(formatDuration(120_000)).toBe('2 min')
    expect(formatDuration(150_000)).toBe('2 min 30 s')
  })

  it('shows a value that is not a finite number as given', () => {
    // Arrange / Act / Assert
    expect(formatDuration(Number.NaN)).toBe('NaN ms')
  })
})

describe('action parameters', () => {
  it('resolve Reusable Objects against the components, as step parameters do', () => {
    // Arrange
    const document: ArazzoDocument = {
      arazzo: '1.1.0',
      components: { parameters: { sku: { name: 'sku', value: '$inputs.sku' } } },
      workflows: [],
    }
    const action: ArazzoFailureAction = {
      name: 'cancelled',
      type: 'goto',
      workflowId: 'placeOrder',
      parameters: [{ reference: '$components.parameters.sku' }, { name: 'quantity', value: 1 }],
    }

    // Act
    const parameters = resolveReusables<ArazzoParameter>(document, action.parameters)

    // Assert
    expect(parameters).toEqual([
      { name: 'sku', value: '$inputs.sku' },
      { name: 'quantity', value: 1 },
    ])
  })
})
