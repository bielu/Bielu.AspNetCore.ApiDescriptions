import type { ArazzoDocument, ArazzoSourceDescription, ArazzoStep } from '../types'
import { findDocumentByUrl, isAsyncApi, isOpenApi, resolveStepTarget, type StepTarget, sourceNameOf } from './resolve-target'

/** The specification a step's target is described in. A step that invokes a workflow targets Arazzo itself. */
export type SpecFamily = 'openapi' | 'asyncapi' | 'arazzo'

/**
 * Which specification a step's target belongs to, so a reader can tell an HTTP call from a message
 * in a workflow that mixes them.
 */
export type TargetSpec = {
  family: SpecFamily
  /** The spec version, from the loaded document (`openapi: 3.1.0`); absent when the document is not loaded. */
  version: string | undefined
  /** Whether the target was found in a loaded document; otherwise the spec comes from the source description. */
  resolved: boolean
  /** The source description the target belongs to, when known. */
  sourceName: string | undefined
}

const SPEC_NAMES: Record<SpecFamily, string> = {
  openapi: 'OpenAPI',
  asyncapi: 'AsyncAPI',
  arazzo: 'Arazzo',
}

/** `OpenAPI 3.1.0`, `AsyncAPI 3.0.0`, or the bare spec name when the version is unknown. */
export const specLabel = (spec: Pick<TargetSpec, 'family' | 'version'>): string =>
  spec.version ? `${SPEC_NAMES[spec.family]} ${spec.version}` : SPEC_NAMES[spec.family]

type AnyRecord = Record<string, unknown>

const isRecord = (value: unknown): value is AnyRecord => !!value && typeof value === 'object'

/** The version the author wrote, when Scalar recorded it, else the document's own field. */
const authoredVersion = (original: unknown, current: unknown): string =>
  typeof original === 'string' && original ? original : String(current)

/**
 * The spec and version a loaded document was written in, or `undefined` when it is neither OpenAPI
 * nor AsyncAPI. Scalar upgrades documents as it loads them (AsyncAPI 3.0 to 3.1, Swagger 2.0 to
 * OpenAPI 3.1) and keeps the authored version in `x-original-oas-version`/`x-original-aas-version`,
 * which is what a reader of the source document expects to see.
 */
export const documentSpec = (document: unknown): Pick<TargetSpec, 'family' | 'version'> | undefined => {
  if (!isRecord(document)) {
    return undefined
  }
  if (isOpenApi(document)) {
    return { family: 'openapi', version: authoredVersion(document['x-original-oas-version'], document.openapi ?? document.swagger) }
  }
  if (isAsyncApi(document)) {
    return { family: 'asyncapi', version: authoredVersion(document['x-original-aas-version'], document.asyncapi) }
  }
  return undefined
}

/** The spec a source description declares through `type` (`openapi`, `asyncapi` or `arazzo`). */
const declaredFamily = (source: ArazzoSourceDescription | undefined): SpecFamily | undefined => {
  const type = source?.type?.toLowerCase()
  return type === 'openapi' || type === 'asyncapi' || type === 'arazzo' ? type : undefined
}

/**
 * The spec a JSON Pointer target implies on its own: `/paths/…` exists only in OpenAPI, while
 * `/channels/…` and `/operations/…` are AsyncAPI.
 */
const pointerFamily = (reference: string | undefined): SpecFamily | undefined => {
  const pointer = reference?.split('#')[1]
  if (!pointer) {
    return undefined
  }
  if (pointer.startsWith('/paths/')) {
    return 'openapi'
  }
  return pointer.startsWith('/channels/') || pointer.startsWith('/operations/') ? 'asyncapi' : undefined
}

/**
 * Works out which specification a step's target belongs to.
 *
 * A resolved target takes the spec and version of the document it was found in. An unresolved one
 * falls back, in order, to the `type` of the source description the step names, to what the pointer
 * implies (`channelPath` is always AsyncAPI), and, for a bare `operationId`, to the one spec every
 * declared source shares. `undefined` when none of these settles it.
 */
export const targetSpec = (
  arazzo: ArazzoDocument,
  step: ArazzoStep,
  target: StepTarget | undefined,
  documents: Record<string, unknown>,
): TargetSpec | undefined => {
  if (!target) {
    return undefined
  }
  if (target.kind === 'workflow') {
    // The version of the document the workflow is in, and the source it was called through, if any.
    return { family: 'arazzo', version: target.version ?? arazzo.arazzo, resolved: Boolean(target.workflow), sourceName: target.sourceName }
  }

  const loaded = target.documentName ? documentSpec(documents[target.documentName]) : undefined
  if (loaded) {
    return { ...loaded, resolved: true, sourceName: target.sourceName }
  }

  const sourceName = target.sourceName ?? sourceNameOf(target.identifier)
  const sources = (arazzo.sourceDescriptions ?? []).filter((source) => declaredFamily(source) !== 'arazzo')
  const named = sourceName ? sources.find((source) => source.name === sourceName) : undefined

  let family = declaredFamily(named) ?? (step.channelPath ? 'asyncapi' : pointerFamily(target.identifier))
  if (!family && !sourceName) {
    const families = new Set(sources.map(declaredFamily))
    const [only] = families
    family = families.size === 1 ? only : undefined
  }
  return family ? { family, version: undefined, resolved: false, sourceName } : undefined
}

/** One source description with what is known about it, for the document header. */
export type SourceSummary = {
  source: ArazzoSourceDescription
  /** From the loaded document when it is loaded, else from the source's `type`. */
  family: SpecFamily | undefined
  version: string | undefined
  /** The workspace document it was found as, when loaded. */
  documentName: string | undefined
  /** How many steps, across all workflows, target this source. */
  stepCount: number
}

/**
 * Summarizes the source descriptions: each one's spec, whether it is loaded, and how many steps use it.
 * A loaded source takes its spec and version from the document; otherwise its `type` is all there is.
 */
export const summarizeSources = (arazzo: ArazzoDocument, documents: Record<string, unknown>): SourceSummary[] => {
  const summaries = new Map<string, SourceSummary>(
    (arazzo.sourceDescriptions ?? []).map((source) => {
      const loaded = findDocumentByUrl(arazzo, documents, source)
      const spec = loaded ? documentSpec(loaded.document) : undefined
      return [
        source.name,
        {
          source,
          family: spec?.family ?? declaredFamily(source),
          version: spec?.version,
          documentName: loaded?.name,
          stepCount: 0,
        },
      ]
    }),
  )

  for (const workflow of arazzo.workflows ?? []) {
    for (const step of workflow.steps ?? []) {
      const target = resolveStepTarget(arazzo, step, documents)
      const spec = targetSpec(arazzo, step, target, documents)
      const summary = spec?.sourceName ? summaries.get(spec.sourceName) : undefined
      if (summary) {
        summary.stepCount++
      }
    }
  }

  return [...summaries.values()]
}

/** How many steps target each spec, in the order OpenAPI, AsyncAPI, Arazzo; specs no step uses are left out. */
export const countStepsBySpec = (
  arazzo: ArazzoDocument,
  documents: Record<string, unknown>,
): { family: SpecFamily; count: number }[] => {
  const counts: Record<SpecFamily, number> = { openapi: 0, asyncapi: 0, arazzo: 0 }
  for (const workflow of arazzo.workflows ?? []) {
    for (const step of workflow.steps ?? []) {
      const spec = targetSpec(arazzo, step, resolveStepTarget(arazzo, step, documents), documents)
      if (spec) {
        counts[spec.family]++
      }
    }
  }
  return (Object.keys(counts) as SpecFamily[])
    .filter((family) => counts[family] > 0)
    .map((family) => ({ family, count: counts[family] }))
}
