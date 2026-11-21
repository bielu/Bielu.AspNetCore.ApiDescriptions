import type { ArazzoCriterion, ArazzoFailureAction, ArazzoSuccessAction } from '../types'
import { deref, operationParameters, type OperationTarget, type StepTarget } from './resolve-target'

type AnyRecord = Record<string, any>

const isRecord = (value: unknown): value is AnyRecord => !!value && typeof value === 'object' && !Array.isArray(value)

const text = (value: unknown): string => (typeof value === 'string' ? value : '')

/** One media type of a request or response body: its schema and first example. */
export type ExchangeContent = {
  contentType: string
  schema: AnyRecord | undefined
  /** The `$ref` the schema was given by (`#/components/schemas/Order`), before it was followed. */
  schemaRef?: string
  example: unknown
}

/** What an OpenAPI operation accepts as its request body. */
export type OperationRequestBody = {
  required: boolean
  description: string
  contents: ExchangeContent[]
  /** The OpenAPI document, which the schemas' `$ref`s resolve against. */
  root: AnyRecord
}

/** One response an OpenAPI operation declares. */
export type OperationResponse = {
  /** `200`, `4XX` or `default`. */
  status: string
  description: string
  contents: ExchangeContent[]
  /** Response header names. */
  headers: string[]
  root: AnyRecord
}

/** The first example of a Media Type Object: `example`, the first of `examples`, or the schema's own. */
const exampleOf = (root: AnyRecord, media: AnyRecord, schema: AnyRecord | undefined): unknown => {
  if (media.example !== undefined) {
    return media.example
  }
  if (isRecord(media.examples)) {
    for (const entry of Object.values(media.examples)) {
      const example = deref(root, entry)
      if (isRecord(example) && example.value !== undefined) {
        return example.value
      }
    }
  }
  return schema?.example
}

/** The media types of an OpenAPI 3 `content` map, references followed. */
const contentsOf = (root: AnyRecord, content: unknown): ExchangeContent[] =>
  isRecord(content)
    ? Object.entries(content).map(([contentType, entry]) => {
        const media = isRecord(entry) ? entry : {}
        const schema = deref(root, media.schema)
        const schemaRef = isRecord(media.schema) && typeof media.schema.$ref === 'string' ? media.schema.$ref : undefined
        return {
          contentType,
          schema: isRecord(schema) ? schema : undefined,
          ...(schemaRef ? { schemaRef } : {}),
          example: exampleOf(root, media, isRecord(schema) ? schema : undefined),
        }
      })
    : []

/** The OpenAPI document an operation target resolved into, when it is loaded. */
const operationRoot = (
  target: StepTarget | undefined,
  documents: Record<string, unknown>,
): { root: AnyRecord; operation: AnyRecord; target: OperationTarget } | undefined => {
  if (target?.kind !== 'operation' || !target.documentName || !isRecord(target.operation)) {
    return undefined
  }
  const root = documents[target.documentName]
  return isRecord(root) ? { root, operation: target.operation, target } : undefined
}

/** Swagger 2.0 media types: the operation's `consumes`/`produces`, else the document's, else JSON. */
const swaggerTypes = (root: AnyRecord, operation: AnyRecord, key: 'consumes' | 'produces'): string[] => {
  const types = Array.isArray(operation[key]) ? operation[key] : Array.isArray(root[key]) ? root[key] : []
  return types.length ? types.filter((type: unknown): type is string => typeof type === 'string') : ['application/json']
}

/**
 * The request body an OpenAPI operation target accepts, references followed in its document: an
 * OpenAPI 3 `requestBody`, or a Swagger 2.0 `in: body` parameter. `undefined` for any other target,
 * one whose document is not loaded, or an operation without a body.
 */
export const operationRequestBody = (target: StepTarget | undefined, documents: Record<string, unknown>): OperationRequestBody | undefined => {
  const resolved = operationRoot(target, documents)
  if (!resolved) {
    return undefined
  }
  const { root, operation } = resolved
  const body = deref(root, operation.requestBody)
  if (isRecord(body)) {
    return { required: body.required === true, description: text(body.description), contents: contentsOf(root, body.content), root }
  }
  // Swagger 2.0 allows one body parameter, declared on the operation or inherited from its path item.
  const bodyParameter = operationParameters(root, resolved.target)
    .filter((parameter) => parameter.in === 'body')
    .pop()
  if (!bodyParameter) {
    return undefined
  }
  const schema = deref(root, bodyParameter.schema)
  return {
    required: bodyParameter.required === true,
    description: text(bodyParameter.description),
    contents: swaggerTypes(root, operation, 'consumes').map((contentType) => ({
      contentType,
      schema: isRecord(schema) ? schema : undefined,
      example: isRecord(schema) ? schema.example : undefined,
    })),
    root,
  }
}

/** `200` before `201` before `2XX` before `4XX` before `default`, as readers scan responses. */
const statusOrder = (status: string): number => {
  if (status === 'default') {
    return 1000
  }
  const code = Number.parseInt(status.replace(/X/gi, '9'), 10)
  return Number.isFinite(code) ? code : 999
}

/**
 * The responses an OpenAPI operation target declares, by status, references followed in its document
 * (OpenAPI 3 `content`, or Swagger 2.0 `schema` with `produces`). Empty for any other target or one
 * whose document is not loaded.
 */
export const operationResponses = (target: StepTarget | undefined, documents: Record<string, unknown>): OperationResponse[] => {
  const resolved = operationRoot(target, documents)
  if (!resolved || !isRecord(resolved.operation.responses)) {
    return []
  }
  const { root, operation } = resolved
  return Object.entries(operation.responses)
    .flatMap(([status, entry]): OperationResponse[] => {
      const response = deref(root, entry)
      if (!isRecord(response)) {
        return []
      }
      const swaggerSchema = deref(root, response.schema)
      const contents = response.content
        ? contentsOf(root, response.content)
        : isRecord(swaggerSchema)
          ? swaggerTypes(root, operation, 'produces').map((contentType) => ({
              contentType,
              schema: swaggerSchema,
              example: isRecord(response.examples) ? response.examples[contentType] : swaggerSchema.example,
            }))
          : []
      return [{ status, description: text(response.description), contents, headers: isRecord(response.headers) ? Object.keys(response.headers) : [], root }]
    })
    .sort((left, right) => statusOrder(left.status) - statusOrder(right.status))
}

/** `$statusCode == 201` (or `201 == $statusCode`): the status codes a list of criteria checks for equality. */
const STATUS_CHECK = /\$statusCode\s*===?\s*(\d{3})|(\d{3})\s*===?\s*\$statusCode/g

/** The status codes a set of criteria compares `$statusCode` to. */
export const statusCodesIn = (criteria: ArazzoCriterion[] | undefined): string[] =>
  (criteria ?? []).flatMap((criterion) =>
    typeof criterion.condition === 'string' ? [...criterion.condition.matchAll(STATUS_CHECK)].map((match) => match[1] ?? match[2]!) : [],
  )

/**
 * The declared response a status code is answered with, as OpenAPI resolves it: the exact status
 * (`422`), else its range (`4XX`), else `default`.
 */
const responseFor = (statuses: string[], code: string): string | undefined =>
  statuses.find((status) => status === code) ??
  statuses.find((status) => /^\dXX$/i.test(status) && status[0] === code[0]) ??
  statuses.find((status) => status === 'default')

/** What a step does with one response: whether its success criteria expect it, and the actions that handle it. */
export type ResponseHandling = {
  success: boolean
  actions: string[]
}

/**
 * For each declared response, what the step does with it: `success` when its success criteria check
 * for that status, and the names of the success or failure actions whose criteria do.
 */
export const responseHandling = (
  responses: OperationResponse[],
  successCriteria: ArazzoCriterion[] | undefined,
  actions: (ArazzoSuccessAction | ArazzoFailureAction)[],
): Record<string, ResponseHandling> => {
  const successCodes = statusCodesIn(successCriteria)
  const actionCodes = actions.map((action) => ({ name: action.name, codes: statusCodesIn(action.criteria) }))
  // Each checked code belongs to one response: `404` to a declared 404, not the 4XX range; `422` with
  // neither declared to `default`.
  const statuses = responses.map((response) => response.status)
  const matches = (status: string, code: string) => responseFor(statuses, code) === status
  return Object.fromEntries(
    responses.map((response) => [
      response.status,
      {
        success: successCodes.some((code) => matches(response.status, code)),
        actions: actionCodes.filter((entry) => entry.codes.some((code) => matches(response.status, code))).map((entry) => entry.name),
      },
    ]),
  )
}
