/** Timer functions, injectable so the schedule can be tested without real time passing. */
export type WatchTimers = {
  setTimeout: (callback: () => void, delay: number) => unknown
  clearTimeout: (handle: unknown) => void
}

export type DocumentWatchOptions = {
  /** Re-reads the documents; returns whether anything changed since the last check. */
  check: () => boolean
  /** Whether every document the Arazzo document needs is loaded, so watching can stop for good. */
  isComplete: () => boolean
  /** Whether the page is hidden; a hidden page is not checked until it is shown again. */
  isHidden?: () => boolean
  /** The first delay, and the delay after any change. */
  minDelay?: number
  /** The longest delay the back-off reaches while nothing changes. */
  maxDelay?: number
  timers?: WatchTimers
}

export type DocumentWatch = {
  /** Checks again soon, with the back-off reset: the page was shown, or something may have loaded. */
  poke: () => void
  stop: () => void
  /** Whether a check is scheduled. */
  readonly active: boolean
}

/**
 * Watches for the sibling documents an Arazzo document's steps resolve into. Scalar adds them to the
 * workspace after the Arazzo document renders, and the object they are added to is reactive only in
 * Scalar's own Vue, so the plugin has to look for itself.
 *
 * The checks start at `minDelay`, double while nothing changes up to `maxDelay`, and drop back to
 * `minDelay` after a change. They stop for good once `isComplete` says every source is loaded, and
 * pause while the page is hidden. A page whose sources are all loaded is not checked at all.
 */
export const watchDocuments = ({
  check,
  isComplete,
  isHidden = () => false,
  minDelay = 1000,
  maxDelay = 10_000,
  timers = { setTimeout: (callback, delay) => setTimeout(callback, delay), clearTimeout: (handle) => clearTimeout(handle as number) },
}: DocumentWatchOptions): DocumentWatch => {
  let delay = minDelay
  let handle: unknown
  let stopped = false

  const schedule = () => {
    if (handle !== undefined) {
      timers.clearTimeout(handle)
      handle = undefined
    }
    if (stopped || isHidden() || isComplete()) {
      return
    }
    handle = timers.setTimeout(tick, delay)
  }

  const tick = () => {
    handle = undefined
    delay = check() ? minDelay : Math.min(delay * 2, maxDelay)
    schedule()
  }

  schedule()

  return {
    poke: () => {
      delay = minDelay
      schedule()
    },
    stop: () => {
      stopped = true
      schedule()
    },
    get active() {
      return handle !== undefined
    },
  }
}
