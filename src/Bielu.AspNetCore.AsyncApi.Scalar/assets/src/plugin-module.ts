import { registerConsoleElement } from './bootstrap'
import { isDocumentRefArray } from './discovery'
import { createConsolePlugin, type ApiReferencePlugin } from './plugin'
import type { ConsoleModuleSpec, DocumentRef } from './types'

/**
 * The ESM entry point of a console bundle, for Scalar's `pluginUrls` configuration.
 *
 * `pluginUrls` is the JSON-serializable way to add a plugin: Scalar `import()`s each URL before it
 * mounts and registers the module's **default export** — which must be a function — as a plugin.
 * That makes it usable from integrations that only pass configuration as JSON (the Aspire AppHost,
 * where the container's HTML is not ours to edit), without replacing Scalar's own bundle.
 *
 * This is deliberately *not* `bootstrapConsole`: that hooks `window.Scalar.createApiReference` to
 * inject the plugin itself, which is the right mechanism for a `<script>` tag but would register the
 * console twice here, since Scalar is already adding this module's default export to `plugins`.
 */

/**
 * Decodes the base64 UTF-8 JSON payload the .NET Aspire packages append to the module URL.
 *
 * `atob` yields one character per *byte*, so a naive `JSON.parse(atob(x))` mangles any non-ASCII
 * text in a document title. Round-tripping through `TextDecoder` restores the original UTF-8.
 */
function decodeBase64Json(value: string): unknown {
  const binary = atob(value)
  const bytes = Uint8Array.from(binary, (character) => character.charCodeAt(0))
  return JSON.parse(new TextDecoder().decode(bytes))
}

/**
 * Reads the console's AsyncAPI documents out of its own module URL
 * (`plugin.mjs?documents=<base64-json>`).
 *
 * A `<script>`-tag bundle reads `document.currentScript`, but that is always `null` inside an ES
 * module, so the module URL — which the caller passes as `import.meta.url` — is the equivalent seam.
 * Malformed input is ignored rather than thrown: the console still works via auto-discovery from the
 * Scalar configuration bound to the element, so a bad query string must not take the page down.
 */
export function documentsFromModuleUrl(moduleUrl: string | undefined): DocumentRef[] {
  if (!moduleUrl) {
    return []
  }
  try {
    const encoded = new URL(moduleUrl).searchParams.get('documents')
    if (!encoded) {
      return []
    }
    const documents = decodeBase64Json(encoded)
    return isDocumentRefArray(documents) ? documents : []
  } catch {
    return []
  }
}

/**
 * Registers the console Web Component and returns the plugin to default-export from a console's
 * ESM entry point.
 *
 * The element must be defined here because the plugin's view renders it by tag name
 * (`spec.elementTag`); unlike the `<script>` path there is no separate bootstrap step to do it.
 */
export function createPluginModule(
  spec: ConsoleModuleSpec,
  component: any,
  moduleUrl?: string,
): ApiReferencePlugin {
  registerConsoleElement(spec, component)
  return createConsolePlugin(spec, documentsFromModuleUrl(moduleUrl))
}
