import type { ArazzoDocument, ArazzoFailureAction, ArazzoStep, ArazzoWorkflow } from '../types'
import { effectiveFailureActions, reachableActions } from './actions'
import { successPath } from './execution-order'
import { formatDuration } from './format'
import { statusCodesIn } from './openapi-exchange'
import { findDocumentByUrl, resolveStepTarget, resolveWorkflow, sourceNameOf } from './resolve-target'
import { targetSpec, type SpecFamily } from './target-spec'

/** A lane of the sequence: the client running the workflow, or a service or broker it talks to. */
export type SequenceParticipant = {
  id: string
  label: string
  /** The spec of a source description's document; the client has none. */
  spec?: SpecFamily
  /** A second line: the spec and the loaded document's title ("OpenAPI · Orders API"). */
  detail?: string
}

/** One arrow of the sequence, in the order the steps run. */
export type SequenceMessage = {
  stepId: string
  from: string
  to: string
  label: string
  /** `request` and `response` for HTTP, `send`/`receive` for messages, `invoke` for a workflow call. */
  kind: 'request' | 'response' | 'send' | 'receive' | 'invoke'
  /** What else the reader should know: how long a receive waits, what it is matched on, how failures end. */
  note?: string
  /** The id of the stage the message belongs to, when its step is one of an invoked workflow's. */
  stage?: string
}

/**
 * A step that invokes a workflow, drawn as a band around that workflow's own messages rather than as one arrow
 * into it, so a workflow composed of others reads as its stages.
 */
export type SequenceStage = {
  /** The invoking step's id, which the stage's messages carry as `stage`. */
  id: string
  /** The invoked workflow's id, as the step names it. */
  workflowId: string
  /** The invoked workflow's summary, or its id. */
  label: string
  /** How the invoking step's failures end. */
  note?: string
}

export type Sequence = {
  participants: SequenceParticipant[]
  messages: SequenceMessage[]
  stages: SequenceStage[]
}

/** The lane of the client running the workflow. */
export const CLIENT_ID = '::client'

export { runOrder } from './execution-order'

/**
 * `$steps.create.outputs.orderId` reads as "orderId from create", and `$inputs.orderId` (as a stage's steps read
 * what the step invoking their workflow passes) as "orderId from inputs"; anything else as written.
 */
const readable = (expression: string): string => {
  const step = /^\$steps\.([^.]+)\.outputs\.([^#]+)/.exec(expression)
  if (step) {
    return `${step[2]} from ${step[1]}`
  }
  const input = /^\$inputs\.([^#]+)/.exec(expression)
  return input ? `${input[1]} from inputs` : expression
}

/** "on 400: rejected (end)", for the failure paths a step can take, kept short. */
const failureNote = (actions: ArazzoFailureAction[]): string | undefined => {
  const notes = actions.slice(0, 3).map((action) => {
    const codes = statusCodesIn(action.criteria)
    const when = codes.length ? `on ${codes.join('/')}` : action.criteria?.length ? 'if' : 'otherwise'
    const where = action.stepId ? `→ ${action.stepId}` : action.workflowId ? `→ ${action.workflowId}` : action.type
    return `${when}: ${action.name} (${where})`
  })
  return notes.length ? [...notes, ...(actions.length > 3 ? ['…'] : [])].join('; ') : undefined
}

/**
 * The workflow as a sequence diagram: who talks to whom, in the order the steps run. The client running the
 * workflow has a lane on the left, then each source description it talks to, in the order first used. An HTTP
 * step is a request to its service and the response its success criteria expect (`201`); an AsyncAPI step
 * publishes to, or is delivered from, its broker lane, with how long it waits and what it is matched on.
 *
 * A step that invokes a workflow is a stage: the invoked workflow's own steps, drawn in place and grouped under
 * its summary. Stages go one level deep, as the step list shows them: a workflow a stage invokes in turn, like one
 * that is not loaded, is a call into that workflow's lane. Only the steps a successful run reaches are drawn, in
 * the order they run (an unconditional `end` stops it, a `goto` jumps); failure paths are noted under the step's
 * arrows rather than drawn, so the main path reads straight down.
 */
export const buildSequence = (document: ArazzoDocument, workflow: ArazzoWorkflow | undefined, documents: Record<string, unknown>): Sequence => {
  const participants: SequenceParticipant[] = [{ id: CLIENT_ID, label: 'Client' }]
  const messages: SequenceMessage[] = []
  const stages: SequenceStage[] = []
  if (!workflow?.steps?.length) {
    return { participants, messages, stages }
  }

  /** The lane for a source description of `owner`, added the first time a step talks to it. */
  const lane = (owner: ArazzoDocument, sourceName: string | undefined, spec: SpecFamily | undefined, fallback: string): string => {
    const id = sourceName ? `source:${sourceName}` : `target:${fallback}`
    if (!participants.some((participant) => participant.id === id)) {
      const source = owner.sourceDescriptions?.find((candidate) => candidate.name === sourceName)
      const loaded = source ? (findDocumentByUrl(owner, documents, source)?.document as { info?: { title?: unknown } } | undefined) : undefined
      const title = typeof loaded?.info?.title === 'string' ? loaded.info.title : undefined
      // Named after the source description, which the steps use and which tells two documents with one title apart.
      const kind = spec === 'openapi' ? 'OpenAPI' : spec === 'asyncapi' ? 'AsyncAPI' : undefined
      const detail = [kind, title].filter(Boolean).join(' · ')
      participants.push({ id, label: sourceName ?? title ?? fallback, ...(spec ? { spec } : {}), ...(detail ? { detail } : {}) })
    }
    return id
  }

  /** Adds a step's messages; `stage` is the invoking step's id when the step is one of an invoked workflow's. */
  const draw = (owner: ArazzoDocument, current: ArazzoWorkflow, step: ArazzoStep, stage: string | undefined): void => {
    const target = resolveStepTarget(owner, step, documents)
    const spec = targetSpec(owner, step, target, documents)
    const failures = failureNote(reachableActions(effectiveFailureActions(owner, current, step)))
    const push = (message: SequenceMessage) => messages.push(stage ? { ...message, stage } : message)
    if (step.workflowId) {
      const invoked = stage ? undefined : resolveWorkflow(owner, step.workflowId, documents)
      if (invoked?.workflow.steps?.length) {
        stages.push({
          id: step.stepId,
          workflowId: step.workflowId,
          label: invoked.workflow.summary ?? invoked.workflow.workflowId,
          ...(failures ? { note: failures } : {}),
        })
        for (const nested of successPath(invoked.document, invoked.workflow)) {
          draw(invoked.document, invoked.workflow, nested, step.stepId)
        }
        return
      }
      const to = `workflow:${step.workflowId}`
      if (!participants.some((participant) => participant.id === to)) {
        participants.push({ id: to, label: `workflow ${step.workflowId.replace(/^\$sourceDescriptions\./, '')}`, spec: 'arazzo', detail: 'Arazzo' })
      }
      push({ stepId: step.stepId, from: CLIENT_ID, to, label: 'runs the workflow', kind: 'invoke', ...(failures ? { note: failures } : {}) })
      return
    }
    const reference = step.operationPath ?? step.channelPath ?? step.operationId ?? step.stepId
    const sourceName = (target && target.kind !== 'workflow' ? target.sourceName : undefined) ?? spec?.sourceName ?? sourceNameOf(reference)
    if (target?.kind === 'channel' || spec?.family === 'asyncapi') {
      const to = lane(owner, sourceName, 'asyncapi', reference)
      const channel = (target?.kind === 'channel' ? target.channelName : undefined) ?? reference.split('/').pop() ?? reference
      const action = step.action ?? (target?.kind === 'channel' ? target.operation?.action : undefined)
      const waits = [step.timeout !== undefined ? `waits up to ${formatDuration(step.timeout)}` : '', step.correlationId ? `matched on ${readable(step.correlationId)}` : '']
        .filter(Boolean)
        .join(', ')
      const note = [waits, failures].filter(Boolean).join('; ') || undefined
      push(
        action === 'receive'
          ? { stepId: step.stepId, from: to, to: CLIENT_ID, label: channel, kind: 'receive', ...(note ? { note } : {}) }
          : { stepId: step.stepId, from: CLIENT_ID, to, label: channel, kind: 'send', ...(note ? { note } : {}) },
      )
      return
    }
    const to = lane(owner, sourceName, spec?.family ?? 'openapi', reference)
    const request =
      target?.kind === 'operation' && target.method && target.path ? `${target.method.toUpperCase()} ${target.path}` : (step.operationId ?? reference)
    push({ stepId: step.stepId, from: CLIENT_ID, to, label: request, kind: 'request' })
    const expected = statusCodesIn(step.successCriteria)[0]
    push({ stepId: step.stepId, from: to, to: CLIENT_ID, label: expected ?? 'response', kind: 'response', ...(failures ? { note: failures } : {}) })
  }

  for (const step of successPath(document, workflow)) {
    draw(document, workflow, step, undefined)
  }
  return { participants, messages, stages }
}
