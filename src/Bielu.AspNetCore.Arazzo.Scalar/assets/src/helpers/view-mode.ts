import { ref } from 'vue'
import type { WorkflowView } from './workflow-graph'

/** Where the reader's choice of view is remembered across reloads (a per-viewer convenience only). */
export const VIEW_STORAGE_KEY = 'bielu-arazzo:view'

const stored = (): WorkflowView => {
  try {
    const value = globalThis.localStorage?.getItem(VIEW_STORAGE_KEY)
    return value === 'pages' || value === 'data' ? value : 'technical'
  } catch {
    // Storage can be blocked (private windows, sandboxed frames): fall back to the default.
    return 'technical'
  }
}

/**
 * The overview's view (Technical, Data flow or Pages), shared by every workflow on the page so switching one switches
 * them all. It starts from the remembered choice, and Technical when there is none.
 */
const current = ref<WorkflowView>(stored())

export const workflowView = (): WorkflowView => current.value

export const setWorkflowView = (view: WorkflowView): void => {
  current.value = view
  try {
    globalThis.localStorage?.setItem(VIEW_STORAGE_KEY, view)
  } catch {
    // Not remembered across reloads, but still shared for this page load.
  }
}

/** Reads the remembered choice again, as a page load does (for tests). */
export const resetWorkflowView = (): void => {
  current.value = stored()
}
