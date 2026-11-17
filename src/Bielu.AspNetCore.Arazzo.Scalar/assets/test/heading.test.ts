import { describe, expect, it } from 'vitest'
import { stepHeading } from '../src/helpers/heading'

describe('stepHeading', () => {
  it.each([
    [{ stepId: 'create', description: 'Create the order.' }, { title: 'Create the order.' }],
    [{ stepId: 'create' }, { title: 'create', description: undefined }],
    [{ stepId: 'create', description: 'Line one\nLine two' }, { title: 'create', description: 'Line one\nLine two' }],
    [{ stepId: 'create', description: 'x'.repeat(101) }, { title: 'create', description: 'x'.repeat(101) }],
  ])('uses a short one-line description as the title: %j', (step, expected) => {
    // Arrange / Act / Assert
    expect(stepHeading(step)).toEqual(expected)
  })
})
