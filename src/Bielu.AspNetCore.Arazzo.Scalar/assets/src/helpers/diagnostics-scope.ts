import { computed, inject, provide, type ComputedRef, type InjectionKey } from 'vue'
import type { Diagnostic } from './diagnostics'

/** `<workflowId>/<stepId>` → that step's diagnostics, provided by the document to every step under it. */
const DIAGNOSTICS: InjectionKey<ComputedRef<Map<string, Diagnostic[]>>> = Symbol('bielu-arazzo:diagnostics')

/** The key a step's diagnostics are filed under. */
export const diagnosticKey = (workflowId: string, stepId: string): string => `${workflowId}/${stepId}`

/** Files the document's diagnostics by step, for the steps and overviews below. */
export const provideDiagnostics = (diagnostics: () => Diagnostic[]): void => {
  provide(
    DIAGNOSTICS,
    computed(() => {
      const byStep = new Map<string, Diagnostic[]>()
      for (const diagnostic of diagnostics()) {
        if (diagnostic.stepId) {
          const key = diagnosticKey(diagnostic.workflowId, diagnostic.stepId)
          byStep.set(key, [...(byStep.get(key) ?? []), diagnostic])
        }
      }
      return byStep
    }),
  )
}

/** The diagnostics by step; empty outside a document. */
export const useDiagnostics = (): ComputedRef<Map<string, Diagnostic[]>> =>
  inject(
    DIAGNOSTICS,
    computed(() => new Map()),
  )

/** The worst severity among some diagnostics, or `undefined` for none. */
export const worstSeverity = (diagnostics: Diagnostic[] | undefined): Diagnostic['severity'] | undefined =>
  diagnostics?.some((diagnostic) => diagnostic.severity === 'error') ? 'error' : diagnostics?.length ? 'warning' : undefined
