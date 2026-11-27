import type { LikeC4Model, LikeC4View, PluginNavigationEntry } from '../types'

/** `PluginNavigationEntry.kind` of a view entry. */
export const VIEW_KIND = 'view'

/** The `_stage` of a model LikeC4 has laid out: the one `likec4 export json` writes, and the one that can be drawn. */
export const LAYOUTED_STAGE = 'layouted'

const isRecord = (value: unknown): value is Record<string, unknown> =>
  typeof value === 'object' && value !== null && !Array.isArray(value)

/**
 * Whether a parsed document is a LikeC4 model as `likec4 export json` writes it: a `_stage`, and `elements`
 * and `views` keyed by id. A model that is not laid out is claimed too, so it gets a note saying how to
 * export it rather than being read as an empty OpenAPI document.
 */
export const isLikeC4Model = (document: Record<string, unknown>): boolean =>
  typeof document._stage === 'string' && isRecord(document.views) && isRecord(document.elements)

/**
 * The model's views, in the order LikeC4 wrote them, with the `index` view (the one LikeC4 opens on) first.
 * Entries that are not views are dropped, so a malformed file shows what it can.
 */
export const viewsOf = (model: LikeC4Model | Record<string, unknown> | undefined): LikeC4View[] => {
  const views = isRecord(model?.views) ? Object.entries(model.views) : []
  const list = views
    .filter((entry): entry is [string, LikeC4View] => isRecord(entry[1]))
    .map(([id, view]) => (typeof view.id === 'string' ? view : { ...view, id }))
  const index = list.findIndex((view) => view.id === 'index')
  return index > 0 ? [list[index]!, ...list.slice(0, index), ...list.slice(index + 1)] : list
}

/** A view's title: its own, else its id. */
export const viewTitle = (view: LikeC4View): string => (typeof view.title === 'string' && view.title.trim()) || view.id

/**
 * Builds the sidebar entries: one per view. The ids are prefixed with `documentId`, as Scalar requires, so
 * they stay unique when several models are loaded.
 */
export const traverseLikeC4Model = ({
  documentId,
  document,
}: {
  documentId: string
  document: Record<string, unknown>
}): PluginNavigationEntry[] =>
  viewsOf(document).map((view) => ({ id: viewEntryId(documentId, view.id), title: viewTitle(view), kind: VIEW_KIND }))

/** The sidebar entry id of a view. */
export const viewEntryId = (documentId: string, viewId: string): string => `${documentId}/view/${viewId}`

/** A view paired with the sidebar entry Scalar scrolls to. */
export type ViewSection = { id: string; view: LikeC4View }

/**
 * Pairs the model's views with the navigation entries Scalar hands back. The content component does not
 * receive `documentId`, so it cannot rebuild the ids itself; both lists come from `viewsOf` over the same
 * document, so they line up by position.
 */
export const toViewSections = (model: LikeC4Model | undefined, navigation: PluginNavigationEntry[]): ViewSection[] => {
  const entries = navigation.filter((entry) => entry.kind === VIEW_KIND)
  return viewsOf(model).flatMap((view, index) => {
    const entry = entries[index]
    return entry ? [{ id: entry.id, view }] : []
  })
}
