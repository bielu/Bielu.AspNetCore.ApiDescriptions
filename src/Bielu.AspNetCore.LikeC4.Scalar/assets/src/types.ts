/**
 * The parts of a LikeC4 model, as `likec4 export json` writes it, that the views are drawn from. Only the
 * fields read here are declared, all optional where LikeC4 may leave them out: the file comes from whichever
 * LikeC4 version the user runs, so everything is checked before it is used.
 */

/** A text LikeC4 keeps either as Markdown or as plain text (`description`, `notes`). */
export type LikeC4Text = string | { md?: string; txt?: string }

/** A rectangle, in the view's coordinates. */
export type LikeC4Box = { x: number; y: number; width: number; height: number }

/** A link on an element or a view. */
export type LikeC4Link = { url: string; title?: string }

/** A node of a laid-out view: an element, placed. */
export type LikeC4Node = LikeC4Box & {
  id: string
  title?: string
  technology?: string | null
  description?: LikeC4Text | null
  kind?: string
  shape?: string
  color?: string
  style?: { opacity?: number; border?: string; size?: string }
  parent?: string | null
  children?: string[]
  depth?: number
  level?: number
  /** The view the element opens, when it has one. */
  navigateTo?: string | null
  links?: LikeC4Link[] | null
  tags?: string[] | null
  /** Where the label sits, relative to the node. */
  labelBBox?: LikeC4Box
}

/** An edge of a laid-out view: one or more relationships, routed. */
export type LikeC4Edge = {
  id: string
  source: string
  target: string
  label?: string | null
  technology?: string | null
  description?: LikeC4Text | null
  notes?: LikeC4Text | null
  /** The route: a cubic Bézier spline, its start point followed by three points per segment. */
  points?: [number, number][]
  /** Where the label sits, in the view's coordinates. */
  labelBBox?: LikeC4Box | null
  color?: string
  line?: 'solid' | 'dashed' | 'dotted' | string
  head?: string
  tail?: string
  navigateTo?: string | null
}

/** A lane of a sequence layout: where the element's box is, and its ports (where steps start and end). */
export type LikeC4SequenceActor = LikeC4Box & {
  id: string
  ports?: { id: string; cx: number; cy: number; height?: number; type?: 'source' | 'target'; position?: 'left' | 'right' }[]
}

/** A dynamic view's sequence layout (`variant sequence`), laid out by LikeC4. */
export type LikeC4SequenceLayout = {
  actors?: LikeC4SequenceActor[]
  steps?: { id: string; sourceHandle: string; targetHandle: string; labelBBox?: { width: number; height: number } }[]
  compounds?: (LikeC4Box & { id: string; origin?: string; depth?: number })[]
  parallelAreas?: (LikeC4Box & { parallelPrefix?: string })[]
  bounds?: LikeC4Box
}

/** A laid-out view. */
export type LikeC4View = {
  id: string
  _type?: 'element' | 'dynamic' | 'deployment' | string
  title?: string | null
  description?: LikeC4Text | null
  /** `diagram` or `sequence`, for dynamic views. */
  variant?: string
  bounds?: LikeC4Box
  nodes?: LikeC4Node[]
  edges?: LikeC4Edge[]
  sequenceLayout?: LikeC4SequenceLayout
  links?: LikeC4Link[] | null
}

/** A custom colour of the specification (`specification { color brand #... }`). */
export type LikeC4CustomColor = { elements?: { fill?: string; stroke?: string; hiContrast?: string } }

/** The model, as `likec4 export json` writes it. */
export type LikeC4Model = {
  /** `layouted` once LikeC4 has laid the views out, which is what `likec4 export json` writes. */
  _stage: string
  projectId?: string
  project?: { id?: string; title?: string }
  specification?: {
    elements?: Record<string, { notation?: string }>
    customColors?: Record<string, LikeC4CustomColor>
  }
  elements?: Record<string, unknown>
  relations?: Record<string, unknown>
  views?: Record<string, LikeC4View>
}

/** A sidebar entry contributed by a plugin document type (`PluginNavigationEntry` in `@scalar/types`). */
export type PluginNavigationEntry = {
  id: string
  title: string
  kind?: string
  children?: PluginNavigationEntry[]
}

/** The workspace event bus subset the plugin emits on. */
export type PluginEventBus = {
  emit: (event: string, payload: { id: string; open?: boolean }) => void
}
