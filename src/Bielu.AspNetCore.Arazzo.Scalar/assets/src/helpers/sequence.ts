import type { ArazzoDocument, ArazzoFailureAction, ArazzoStep, ArazzoWorkflow } from '../types'
import { effectiveFailureActions, reachableActions } from './actions'
import { stepsBefore, successPath, usesDependsOn } from './execution-order'
import { formatDuration } from './format'
import { statusCodesIn } from './openapi-exchange'
import { findDocumentByUrl, resolveStepTarget, resolveWorkflow, sourceNameOf } from './resolve-target'
import { targetSpec, type SpecFamily } from './target-spec'

/** A lane of the sequence: the client running the workflow, a service or broker it talks to, or an actor. */
export type SequenceParticipant = {
  id: string
  label: string
  /** The spec of a source description's document; the client has none. */
  spec?: SpecFamily
  /** A second line: the spec and the loaded document's title ("OpenAPI · Orders API"). */
  detail?: string
  /** What the actor registry says about the lane, for its tooltip. */
  description?: string
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

/** How the sequence is drawn: the plugin's options it reads. */
export type SequenceOptions = {
  /** The specification extension (such as `x-actor`) naming who makes a step's call; the client when unset. */
  actorExtension?: string
  /**
   * The document-level extension (such as `x-actors`) holding the actor registry: actor name →
   * `{ title, description, client }`. It titles lanes, puts the lanes it lists first in its order, and names the
   * client (`client: true`), whose lane the steps without an actor are on.
   */
  actorsExtension?: string
}

/** What the actor registry says about one actor. */
export type SequenceActor = { title?: string; description?: string; client?: boolean }

const text = (value: unknown): string | undefined => (typeof value === 'string' && value.trim() ? value.trim() : undefined)

/** The actor registry of a document, in its order; entries that are not objects are left out. */
export const actorRegistry = (document: ArazzoDocument, extension: string | undefined): Map<string, SequenceActor> => {
  const value = extension ? (document as Record<string, unknown>)[extension] : undefined
  const registry = new Map<string, SequenceActor>()
  if (value && typeof value === 'object' && !Array.isArray(value)) {
    for (const [name, entry] of Object.entries(value as Record<string, unknown>)) {
      if (entry && typeof entry === 'object' && !Array.isArray(entry)) {
        const { title, description, client } = entry as Record<string, unknown>
        registry.set(name, {
          ...(text(title) ? { title: text(title) } : {}),
          ...(text(description) ? { description: text(description) } : {}),
          ...(client === true ? { client: true } : {}),
        })
      }
    }
  }
  return registry
}

/**
 * Steps that run at the same time, drawn as a `par` fragment with a branch per step: consecutive steps (in run
 * order) of a workflow that uses `dependsOn`, none of which waits on another.
 */
export type SequenceParallel = {
  id: string
  /** The stage the steps are in (the step invoking their workflow), or none for the workflow's own steps. */
  scope?: string
  /** The steps, in run order; one may be a stage (its id is the stage's). */
  steps: string[]
}

export type Sequence = {
  participants: SequenceParticipant[]
  messages: SequenceMessage[]
  stages: SequenceStage[]
  parallels: SequenceParallel[]
}

/**
 * Groups a workflow's steps, in the order they run, into runs of steps that run at the same time: a step joins the
 * group before it when it waits on none of its steps. Without `dependsOn` a workflow runs one step at a time.
 */
export const parallelGroups = (document: ArazzoDocument, workflow: ArazzoWorkflow, steps: ArazzoStep[]): ArazzoStep[][] => {
  if (!usesDependsOn(workflow.steps ?? [])) {
    return steps.map((step) => [step])
  }
  const groups: ArazzoStep[][] = []
  for (const step of steps) {
    const last = groups[groups.length - 1]
    const before = stepsBefore(document, workflow, step)
    if (last && last.every((other) => !before.has(other.stepId))) {
      last.push(step)
    } else {
      groups.push([step])
    }
  }
  return groups
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
 *
 * Arazzo has no field for who makes a step's call: its runner makes them all, so every arrow starts at the client.
 * With `actorExtension` (such as `x-actor`), a step, or the workflow it belongs to, can name who does instead:
 * a source description (its lane), or anyone else (a lane of their own). The step's request, message or workflow
 * call then starts from that lane, and its response or delivered message goes back to it. A stage's steps take
 * the actor of the step invoking the workflow, when neither they nor their workflow name one. The client's lane
 * is left out when no step is the client's.
 */
export const buildSequence = (
  document: ArazzoDocument,
  workflow: ArazzoWorkflow | undefined,
  documents: Record<string, unknown>,
  options: SequenceOptions = {},
): Sequence => {
  const registry = actorRegistry(document, options.actorsExtension)
  const [clientName, client] = [...registry].find(([, entry]) => entry.client) ?? []
  const participants: SequenceParticipant[] = [
    { id: CLIENT_ID, label: client?.title ?? clientName ?? 'Client', ...(client?.description ? { description: client.description } : {}) },
  ]
  const messages: SequenceMessage[] = []
  const stages: SequenceStage[] = []
  const parallels: SequenceParallel[] = []
  if (!workflow?.steps?.length) {
    return { participants, messages, stages, parallels }
  }

  /** Draws a workflow's steps in the order they run, noting the ones that run at the same time. */
  const drawAll = (owner: ArazzoDocument, current: ArazzoWorkflow, stage: string | undefined, inherited?: () => string): void => {
    for (const group of parallelGroups(owner, current, successPath(owner, current))) {
      if (group.length > 1) {
        parallels.push({ id: `${stage ?? ''}/${group[0]!.stepId}`, ...(stage ? { scope: stage } : {}), steps: group.map((step) => step.stepId) })
      }
      for (const step of group) {
        draw(owner, current, step, stage, inherited)
      }
    }
  }

  /** The actor an object names with the actor extension, if it names one. */
  const actorOf = (of: object): string | undefined => (options.actorExtension ? text((of as Record<string, unknown>)[options.actorExtension]) : undefined)

  /** The lane of an actor: its source description's when it names one of `owner`'s, its own otherwise. */
  const actorLane = (owner: ArazzoDocument, actor: string | undefined): string => {
    if (!actor || actor === clientName) {
      return CLIENT_ID
    }
    const source = owner.sourceDescriptions?.find((candidate) => candidate.name === actor)
    if (source) {
      const spec = source.type === 'asyncapi' || source.type === 'arazzo' ? source.type : 'openapi'
      return lane(owner, source.name, spec, actor)
    }
    const id = `actor:${actor}`
    if (!participants.some((participant) => participant.id === id)) {
      participants.push({ id, label: actor })
    }
    return id
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

  /**
   * Adds a step's messages; `stage` is the invoking step's id when the step is one of an invoked workflow's, and
   * `inherited` gives the lane of that step's actor. An actor the step or its workflow names is looked up in the
   * step's own document; an inherited one keeps the invoking step's lane, even when the invoked workflow is in
   * another document that has no source by that name.
   */
  const draw = (owner: ArazzoDocument, current: ArazzoWorkflow, step: ArazzoStep, stage: string | undefined, inherited?: () => string): void => {
    const named = actorOf(step) ?? actorOf(current)
    /** The lane the step's calls start from, added when first needed so lanes keep the order they are used in. */
    const by = (): string => (named ? actorLane(owner, named) : inherited ? inherited() : CLIENT_ID)
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
        drawAll(invoked.document, invoked.workflow, step.stepId, by)
        return
      }
      const from = by()
      const to = `workflow:${step.workflowId}`
      if (!participants.some((participant) => participant.id === to)) {
        participants.push({ id: to, label: `workflow ${step.workflowId.replace(/^\$sourceDescriptions\./, '')}`, spec: 'arazzo', detail: 'Arazzo' })
      }
      push({ stepId: step.stepId, from, to, label: 'runs the workflow', kind: 'invoke', ...(failures ? { note: failures } : {}) })
      return
    }
    const reference = step.operationPath ?? step.channelPath ?? step.operationId ?? step.stepId
    const sourceName = (target && target.kind !== 'workflow' ? target.sourceName : undefined) ?? spec?.sourceName ?? sourceNameOf(reference)
    if (target?.kind === 'channel' || spec?.family === 'asyncapi') {
      const action = step.action ?? (target?.kind === 'channel' ? target.operation?.action : undefined)
      // Lanes are added in the order a message passes them: the sender's before the broker's, and the broker's
      // before the lane it delivers to.
      const sender = action === 'receive' ? undefined : by()
      const broker = lane(owner, sourceName, 'asyncapi', reference)
      const self = sender ?? by()
      const channel = (target?.kind === 'channel' ? target.channelName : undefined) ?? reference.split('/').pop() ?? reference
      const waits = [step.timeout !== undefined ? `waits up to ${formatDuration(step.timeout)}` : '', step.correlationId ? `matched on ${readable(step.correlationId)}` : '']
        .filter(Boolean)
        .join(', ')
      const note = [waits, failures].filter(Boolean).join('; ') || undefined
      push(
        action === 'receive'
          ? { stepId: step.stepId, from: broker, to: self, label: channel, kind: 'receive', ...(note ? { note } : {}) }
          : { stepId: step.stepId, from: self, to: broker, label: channel, kind: 'send', ...(note ? { note } : {}) },
      )
      return
    }
    const from = by()
    const to = lane(owner, sourceName, spec?.family ?? 'openapi', reference)
    const request =
      target?.kind === 'operation' && target.method && target.path ? `${target.method.toUpperCase()} ${target.path}` : (step.operationId ?? reference)
    push({ stepId: step.stepId, from, to, label: request, kind: 'request' })
    const expected = statusCodesIn(step.successCriteria)[0]
    push({ stepId: step.stepId, from: to, to: from, label: expected ?? 'response', kind: 'response', ...(failures ? { note: failures } : {}) })
  }

  drawAll(document, workflow, undefined)
  // Every step names an actor: nobody calls from the client's lane, so it is not drawn.
  const clientUsed = messages.some((message) => message.from === CLIENT_ID || message.to === CLIENT_ID)
  // The registry titles the lanes it lists (a source's lane is listed by the source's name), and puts them first,
  // in its order; the client stays leftmost, and the other lanes keep the order they were first used in.
  const names = [...registry.keys()]
  const nameOf = (id: string) => /^(?:source|actor):(.*)$/.exec(id)?.[1]
  const rank = (id: string) => {
    const index = id === CLIENT_ID ? -1 : names.indexOf(nameOf(id) ?? '')
    return index < 0 && id !== CLIENT_ID ? names.length : index
  }
  const lanes = (clientUsed ? participants : participants.slice(1))
    .map((participant, index) => {
      const entry = participant.id === CLIENT_ID ? undefined : registry.get(nameOf(participant.id) ?? '')
      return {
        index,
        participant: {
          ...participant,
          ...(entry?.title ? { label: entry.title } : {}),
          ...(entry?.description ? { description: entry.description } : {}),
        },
      }
    })
    .sort((a, b) => rank(a.participant.id) - rank(b.participant.id) || a.index - b.index)
    .map(({ participant }) => participant)
  return { participants: lanes, messages, stages, parallels }
}
