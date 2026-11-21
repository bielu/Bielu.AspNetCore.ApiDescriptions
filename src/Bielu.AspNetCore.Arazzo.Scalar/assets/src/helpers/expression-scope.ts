import { computed, inject, provide, type ComputedRef, type InjectionKey } from 'vue'
import type { ExpressionScope } from './expressions'

/**
 * The scope Runtime Expressions are read in, provided by the workflow (or the components section) to
 * every value, condition and payload under it, so they link without each component passing it on.
 */
const EXPRESSION_SCOPE: InjectionKey<ComputedRef<ExpressionScope | undefined>> = Symbol('bielu-arazzo:expression-scope')

/** Provides the scope the components below read expressions in. */
export const provideExpressionScope = (scope: () => ExpressionScope | undefined): void => {
  provide(EXPRESSION_SCOPE, computed(scope))
}

/** The scope a component's expressions are read in; `undefined` outside a workflow or the components section. */
export const useExpressionScope = (): ComputedRef<ExpressionScope | undefined> =>
  inject(
    EXPRESSION_SCOPE,
    computed(() => undefined),
  )
