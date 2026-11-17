import { describe, expect, it } from 'vitest'
import { watchDocuments, type WatchTimers } from '../src/helpers/document-watch'

/** Manual timers: `run()` fires the pending callback and returns the delay it was scheduled with. */
const manualTimers = () => {
  let pending: { callback: () => void; delay: number } | undefined
  const timers: WatchTimers = {
    setTimeout: (callback, delay) => {
      pending = { callback, delay }
      return pending
    },
    clearTimeout: (handle) => {
      if (handle === pending) {
        pending = undefined
      }
    },
  }
  return {
    timers,
    get delay() {
      return pending?.delay
    },
    run: () => {
      const current = pending
      pending = undefined
      current?.callback()
    },
  }
}

describe('watchDocuments', () => {
  it('backs off while nothing changes and drops back after a change', () => {
    // Arrange
    const clock = manualTimers()
    const changes = [false, false, false, true, false]
    const delays: (number | undefined)[] = []

    // Act
    watchDocuments({ check: () => changes.shift() ?? false, isComplete: () => false, minDelay: 100, maxDelay: 500, timers: clock.timers })
    for (let index = 0; index < 5; index++) {
      delays.push(clock.delay)
      clock.run()
    }
    delays.push(clock.delay)

    // Assert
    expect(delays).toEqual([100, 200, 400, 500, 100, 200])
  })

  it('stops for good once every source is loaded', () => {
    // Arrange
    const clock = manualTimers()
    let complete = false
    const watch = watchDocuments({
      check: () => {
        complete = true
        return true
      },
      isComplete: () => complete,
      timers: clock.timers,
    })

    // Act
    clock.run()
    watch.poke()

    // Assert
    expect(watch.active).toBe(false)
    expect(clock.delay).toBeUndefined()
  })

  it('never starts when the sources are already loaded', () => {
    // Arrange
    const clock = manualTimers()

    // Act
    const watch = watchDocuments({ check: () => false, isComplete: () => true, timers: clock.timers })

    // Assert
    expect(watch.active).toBe(false)
  })

  it('pauses while the page is hidden and resumes, with the back-off reset, when poked', () => {
    // Arrange
    const clock = manualTimers()
    let hidden = false
    const watch = watchDocuments({
      check: () => false,
      isComplete: () => false,
      isHidden: () => hidden,
      minDelay: 100,
      timers: clock.timers,
    })
    clock.run()
    hidden = true

    // Act
    clock.run()
    const whileHidden = watch.active
    hidden = false
    watch.poke()

    // Assert
    expect(whileHidden).toBe(false)
    expect(watch.active).toBe(true)
    expect(clock.delay).toBe(100)
  })

  it('cancels the scheduled check when stopped', () => {
    // Arrange
    const clock = manualTimers()
    const watch = watchDocuments({ check: () => false, isComplete: () => false, timers: clock.timers })

    // Act
    watch.stop()
    watch.poke()

    // Assert
    expect(watch.active).toBe(false)
    expect(clock.delay).toBeUndefined()
  })
})
