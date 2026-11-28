import { describe, expect, it } from 'vitest'
import { lineColorOf, paletteOf } from '../src/helpers/colors'
import { arrowHead } from '@bielu/scalar-graph'
import { dashArray, edgeArrows, edgePath, queueCap, shapeGeometry, wrapText } from '../src/helpers/geometry'

describe('shapeGeometry', () => {
  it.each(['rectangle', 'person', 'queue', 'cylinder', 'storage', 'browser', 'mobile', 'component', 'bucket', 'document'])(
    'draws a closed %s outline',
    (shape) => {
      // Act
      const geometry = shapeGeometry(shape, 320, 180)

      // Assert
      expect(geometry.body).toMatch(/^M/)
      expect(geometry.body.trim().endsWith('Z')).toBe(true)
    },
  )

  it('draws a shape LikeC4 may add later as a rectangle', () => {
    // Act
    const geometry = shapeGeometry('hexagon-of-the-future', 100, 50)

    // Assert
    expect(geometry).toEqual(shapeGeometry('rectangle', 100, 50))
    expect(geometry.details).toEqual([])
  })

  it('gives a person a head and a queue its front end', () => {
    // Act
    const person = shapeGeometry('person', 320, 180)
    const queue = shapeGeometry('queue', 320, 180)

    // Assert
    expect(person.details).toHaveLength(1)
    expect(queue.details).toHaveLength(1)
    expect(queueCap(320, 180)).toBe(32.4)
  })
})

describe('edgePath', () => {
  it('draws a Graphviz spline as cubic Bézier segments', () => {
    // Act
    const path = edgePath([
      [0, 0],
      [10, 0],
      [20, 10],
      [30, 10],
      [40, 10],
      [50, 20],
      [60, 20],
    ])

    // Assert
    expect(path).toBe('M0,0 C10,0 20,10 30,10 C40,10 50,20 60,20')
  })

  it('draws a route that is not a spline through its points', () => {
    // Act
    const path = edgePath([
      [0, 0],
      [10, 5],
      [20, 0],
    ])

    // Assert
    expect(path).toBe('M0,0 L10,5 L20,0')
  })

  it('draws nothing without a route', () => {
    // Act & Assert
    expect(edgePath(undefined)).toBe('')
    expect(edgePath([[1, 1]])).toBe('')
  })
})

describe('arrowHead', () => {
  it('points a filled triangle along the line, its tip on the end point', () => {
    // Act
    const arrow = arrowHead({ x: 0, y: 0 }, { x: 100, y: 0 }, 'normal')

    // Assert
    expect(arrow).toEqual({ path: 'M100,0 L90,4.5 L90,-4.5 Z', filled: true })
  })

  it('outlines an onormal head, draws a vee open, and leaves out none', () => {
    // Act & Assert
    expect(arrowHead({ x: 0, y: 0 }, { x: 0, y: 100 }, 'onormal')?.filled).toBe(false)
    expect(arrowHead({ x: 0, y: 0 }, { x: 0, y: 100 }, 'vee')?.path).not.toContain('Z')
    expect(arrowHead({ x: 0, y: 0 }, { x: 0, y: 100 }, 'diamond')).toMatchObject({ filled: true })
    expect(arrowHead({ x: 0, y: 0 }, { x: 0, y: 100 }, 'odiamond')).toMatchObject({ filled: false })
    expect(arrowHead({ x: 0, y: 0 }, { x: 0, y: 100 }, 'none')).toBeUndefined()
  })

  it('draws a crow head as an open crow foot, its prongs spreading at the end point', () => {
    // Act
    const arrow = arrowHead({ x: 0, y: 0 }, { x: 20, y: 0 }, 'crow')

    // Assert
    expect(arrow).toEqual({ path: 'M20,4.5 L10,0 L20,-4.5 M10,0 L20,0', filled: false })
  })

  it('still points somewhere when the line has no length', () => {
    // Act
    const arrow = arrowHead({ x: 5, y: 5 }, { x: 5, y: 5 }, undefined)

    // Assert
    expect(arrow?.path).toBe('M5,5 L-5,9.5 L-5,0.5 Z')
  })
})

describe('edgeArrows', () => {
  it('draws a head at the target, and a tail at the source when the edge has one', () => {
    // Arrange
    const points: [number, number][] = [
      [0, 0],
      [50, 0],
    ]

    // Act & Assert
    expect(edgeArrows(points, 'normal', undefined)).toHaveLength(1)
    expect(edgeArrows(points, 'normal', 'onormal')).toHaveLength(2)
    expect(edgeArrows(points, 'none', 'none')).toHaveLength(0)
    expect(edgeArrows([[0, 0]], 'normal', 'normal')).toHaveLength(0)
  })
})

describe('dashArray', () => {
  it('dashes and dots lines as LikeC4 does, and leaves solid lines solid', () => {
    // Act & Assert
    expect(dashArray('dashed')).toBe('6 5')
    expect(dashArray('dotted')).toBe('1.5 4')
    expect(dashArray('solid')).toBeUndefined()
    expect(dashArray(undefined)).toBeUndefined()
  })
})

describe('wrapText', () => {
  it('breaks between words to fit the width', () => {
    // Act
    const lines = wrapText('Takes orders and tells the rest of the shop', 100, 10)

    // Assert
    expect(lines).toEqual(['Takes orders and', 'tells the rest of', 'the shop'])
  })

  it('breaks a word longer than a line', () => {
    // Act
    const lines = wrapText('notifications/inventory-alerts', 57, 10)

    // Assert
    expect(lines).toEqual(['notificati', 'ons/invent', 'ory-alerts'])
  })

  it('ends the last kept line with an ellipsis when text is left over', () => {
    // Act
    const lines = wrapText('one two three four five six', 40, 10, 2)

    // Assert
    expect(lines).toEqual(['one two', 'three…'])
  })

  it('returns no lines for empty text', () => {
    // Act & Assert
    expect(wrapText('  ', 100, 10)).toEqual([])
  })
})

describe('paletteOf', () => {
  it('tints the hue onto Scalar background, primary following the theme accent', () => {
    // Act
    const palette = paletteOf('primary')

    // Assert
    expect(palette.stroke).toBe('var(--scalar-color-accent, #3b82f6)')
    expect(palette.fill).toBe('color-mix(in srgb, var(--scalar-color-accent, #3b82f6) 16%, var(--scalar-background-1, #fff))')
  })

  it('uses a custom colour of the specification, and only a CSS colour', () => {
    // Arrange
    const custom = { brand: { elements: { fill: '#ff00aa' } }, bad: { elements: { fill: 'red;} body{display:none' } } }

    // Act & Assert
    expect(paletteOf('brand', custom).stroke).toBe('#ff00aa')
    expect(paletteOf('bad', custom).stroke).toBe('var(--scalar-color-accent, #3b82f6)')
    expect(paletteOf('unknown').stroke).toBe('var(--scalar-color-accent, #3b82f6)')
  })

  it('takes a container tint and keeps it within 0 to 100 percent', () => {
    // Act & Assert
    expect(paletteOf('red', undefined, 150).fill).toContain('#ef4444 100%')
    expect(paletteOf('red', undefined, -5).fill).toContain('#ef4444 0%')
  })
})

describe('lineColorOf', () => {
  it('draws gray relationships in Scalar muted text colour, and others in their hue', () => {
    // Act & Assert
    expect(lineColorOf('gray')).toBe('var(--scalar-color-3, #737373)')
    expect(lineColorOf(undefined)).toBe('var(--scalar-color-3, #737373)')
    expect(lineColorOf('green')).toBe('#22c55e')
  })
})
