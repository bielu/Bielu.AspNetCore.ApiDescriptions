import type { ArazzoDocument, ArazzoStep } from '../types'
import { formatDuration } from './format'
import { parseJsonPointer, resolveStepTarget, SOURCE_DESCRIPTION_POINTER } from './resolve-target'
import type { StepDescription } from './workflow-graph'

/** `$steps.create.outputs.orderId` reads as "orderId from create"; anything else as written. */
const readable = (expression: string): string => {
  const match = /^\$steps\.([^.]+)\.outputs\.([^.#]+)/.exec(expression)
  return match ? `${match[2]} from ${match[1]}` : expression
}

/** The channel a `channelPath` names (`…#/channels/orderPlaced` → `orderPlaced`), for when its document is not loaded. */
const channelOfPath = (path: string | undefined): string | undefined => {
  const pointer = path ? SOURCE_DESCRIPTION_POINTER.exec(path)?.[2] : undefined
  const segments = pointer ? parseJsonPointer(pointer) : []
  return segments[0] === 'channels' ? segments[1] : undefined
}

/**
 * What a step's box in the overview says about its target, so a reader sees when a step runs without opening
 * it. An OpenAPI step: its method and path (`POST /orders`). An AsyncAPI step: whether it publishes or waits
 * for a message, on which channel, and how long it waits (`← receives orderPlaced · 15 s`), with the
 * correlation it waits for in the tooltip. `undefined` for a workflow step, or a target it cannot tell.
 */
export const describeStep = (step: ArazzoStep, document: ArazzoDocument, documents: Record<string, unknown>): StepDescription | undefined => {
  const target = resolveStepTarget(document, step, documents)
  if (!target || target.kind === 'workflow') {
    return undefined
  }
  if (target.kind === 'operation') {
    if (!target.method || !target.path) {
      return undefined
    }
    const request = `${target.method.toUpperCase()} ${target.path}`
    return { detail: request, explanation: `Calls ${request}` }
  }

  const channel = target.channelName ?? channelOfPath(step.channelPath) ?? target.identifier
  const action = step.action ?? target.operation?.action
  const timeout = typeof step.timeout === 'number' ? formatDuration(step.timeout) : undefined
  const correlation = step.correlationId ? readable(step.correlationId) : undefined
  if (action === 'send') {
    return {
      detail: `→ sends ${channel}`,
      explanation: `Publishes a message on ${channel}${target.channel?.address ? ` (${target.channel.address})` : ''}`,
    }
  }
  if (action === 'receive') {
    const waits = [
      `Waits for a message on ${channel}${target.channel?.address ? ` (${target.channel.address})` : ''}`,
      correlation ? `, the one matching ${correlation}` : '',
      timeout ? `; fails after ${timeout}` : '',
    ].join('')
    return { detail: [`← receives ${channel}`, timeout].filter(Boolean).join(' · '), explanation: waits }
  }
  return { detail: `↔ ${channel}`, explanation: `Uses channel ${channel}` }
}
