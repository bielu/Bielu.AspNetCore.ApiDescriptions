import type { ArazzoStep } from '../types'

/** Longer than this, a description reads as prose rather than a title. */
export const MAX_TITLE_LENGTH = 100

/**
 * The heading of a step and the description left to show under it. Steps have no title field, so a
 * short single-line description becomes the heading, as in Scalar's in-core POC, and is not repeated
 * below. Anything longer stays a description under the `stepId`.
 */
export const stepHeading = (step: Pick<ArazzoStep, 'stepId' | 'description'>): { title: string; description?: string } => {
  const description = step.description?.trim()
  if (description && !description.includes('\n') && description.length <= MAX_TITLE_LENGTH) {
    return { title: description }
  }
  return { title: step.stepId, description }
}
