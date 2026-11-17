import { readFileSync } from 'node:fs'
import { describe, expect, it } from 'vitest'
import { asSelector, criterionTypeLabel } from '../src/helpers/format'
import type { ArazzoDocument } from '../src/types'

describe('asSelector', () => {
  it('recognizes a Selector Object with a string or object type', () => {
    // Arrange
    const simple = { context: '$response.body', selector: '$.id', type: 'jsonpath' }
    const typed = { context: '$inputs', selector: '$.quantity', type: { type: 'jsonpath', version: 'rfc9535' } }

    // Act / Assert
    expect(asSelector(simple)).toBe(simple)
    expect(asSelector(typed)).toBe(typed)
  })

  it('leaves constants, Runtime Expressions and look-alikes alone', () => {
    // Arrange / Act / Assert
    expect(asSelector('$response.body#/id')).toBeUndefined()
    expect(asSelector(42)).toBeUndefined()
    expect(asSelector(null)).toBeUndefined()
    expect(asSelector([{ context: 'a', selector: 'b', type: 'jsonpath' }])).toBeUndefined()
    expect(asSelector({ context: '$inputs', selector: '$.x' })).toBeUndefined()
    expect(asSelector({ context: 1, selector: '$.x', type: 'jsonpath' })).toBeUndefined()
  })

  it('labels a selector and a replacement target the same way as a criterion', () => {
    // Arrange / Act / Assert
    expect(criterionTypeLabel('jsonpointer')).toBe('jsonpointer')
    expect(criterionTypeLabel({ type: 'xpath', version: 'xpath-30' })).toBe('xpath xpath-30')
  })
})

describe('field reference fixture', () => {
  // The example serves this document so the view can be checked against every Arazzo 1.1 field.
  const fixture = JSON.parse(
    readFileSync(new URL('../../../examples/ArazzoScalar/wwwroot/fixtures/field-reference.arazzo.json', import.meta.url), 'utf8'),
  ) as ArazzoDocument

  it('uses selectors in outputs and replacements, and a selector-typed replacement target', () => {
    // Arrange
    const create = fixture.workflows![0]!.steps![0]!
    const replacements = create.requestBody!.replacements!

    // Act / Assert
    expect(asSelector(create.outputs!.firstLine)).toBeDefined()
    expect(asSelector(replacements[2]!.value)).toBeDefined()
    expect(criterionTypeLabel(replacements[1]!.targetSelectorType)).toBe('jsonpath')
  })
})
