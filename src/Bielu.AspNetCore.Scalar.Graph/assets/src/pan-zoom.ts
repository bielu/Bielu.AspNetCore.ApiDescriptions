/** Where the diagram canvas sits inside its viewport, and how far it is zoomed. */
export type PanZoomState = {
  x: number
  y: number
  scale: number
}

export const MIN_SCALE = 0.5
export const MAX_SCALE = 4

export const INITIAL_PAN_ZOOM: PanZoomState = { x: 0, y: 0, scale: 1 }

export const clampScale = (scale: number): number => Math.min(MAX_SCALE, Math.max(MIN_SCALE, scale))

/**
 * Zooms around `pivot` (in the same coordinates as `state.x`/`state.y`), so the point under the
 * cursor stays where it is instead of the diagram scaling toward its own top-left corner.
 */
export const zoomAt = (state: PanZoomState, direction: 'in' | 'out', pivot: { x: number; y: number }): PanZoomState => {
  const scale = clampScale(state.scale * (direction === 'in' ? 1.1 : 1 / 1.1))
  if (scale === state.scale) {
    return state
  }
  const ratio = scale / state.scale
  return {
    scale,
    x: pivot.x - (pivot.x - state.x) * ratio,
    y: pivot.y - (pivot.y - state.y) * ratio,
  }
}

export const panBy = (state: PanZoomState, dx: number, dy: number): PanZoomState => ({
  ...state,
  x: state.x + dx,
  y: state.y + dy,
})

/** How far one arrow key press pans the diagram, in pixels. */
export const KEYBOARD_PAN_STEP = 40

/**
 * The keyboard equivalent of drag, wheel and double-click, for a focused diagram: the arrow keys
 * look around (the view moves the way the arrow points), `+`/`-` zoom around `center`, and `0`
 * resets. `undefined` for any other key, which is left to the page.
 */
export const panZoomForKey = (
  state: PanZoomState,
  key: string,
  center: { x: number; y: number },
): PanZoomState | undefined => {
  switch (key) {
    case 'ArrowLeft':
      return panBy(state, KEYBOARD_PAN_STEP, 0)
    case 'ArrowRight':
      return panBy(state, -KEYBOARD_PAN_STEP, 0)
    case 'ArrowUp':
      return panBy(state, 0, KEYBOARD_PAN_STEP)
    case 'ArrowDown':
      return panBy(state, 0, -KEYBOARD_PAN_STEP)
    case '+':
    case '=':
      return zoomAt(state, 'in', center)
    case '-':
    case '_':
      return zoomAt(state, 'out', center)
    case '0':
      return { ...INITIAL_PAN_ZOOM }
    default:
      return undefined
  }
}
