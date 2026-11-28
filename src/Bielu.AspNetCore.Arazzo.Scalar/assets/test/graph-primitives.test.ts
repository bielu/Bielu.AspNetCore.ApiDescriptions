import { roundedPolylinePath, truncate } from '@bielu/scalar-graph'
import { describe, expect, it } from 'vitest'

// The @bielu/scalar-graph primitives the workflow overview draws with (the package has no test job of its own).
describe('roundedPolylinePath', () => {
  it('rounds each bend with a curve through the middle of the next segment', () => {
    // Act
    const path = roundedPolylinePath([
      { x: 0, y: 0 },
      { x: 0, y: 10 },
      { x: 10, y: 10 },
      { x: 10, y: 20 },
    ])

    // Assert
    expect(path).toBe('M0,0 Q0,10 5,10 Q10,10 10,15 L10,20')
  })

  it('draws two points as a straight line, and nothing for no points', () => {
    // Act & Assert
    expect(roundedPolylinePath([{ x: 1, y: 2 }, { x: 3, y: 4 }])).toBe('M1,2 L3,4')
    expect(roundedPolylinePath([{ x: 1, y: 2 }])).toBe('M1,2')
    expect(roundedPolylinePath([])).toBe('')
  })
})

describe('truncate', () => {
  it('cuts text longer than the limit, ending it with an ellipsis', () => {
    // Act & Assert
    expect(truncate('abcdef', 4)).toBe('abc…')
    expect(truncate('abcd', 4)).toBe('abcd')
  })
})
