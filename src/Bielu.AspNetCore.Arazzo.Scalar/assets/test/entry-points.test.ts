import { readFileSync } from 'node:fs'
import { beforeEach, describe, expect, it, vi } from 'vitest'

const core = vi.hoisted(() => ({ registerConsoleElement: vi.fn(), installScalarPluginHook: vi.fn() }))
vi.mock('@bielu/scalar-core', () => core)

const packageJson = JSON.parse(readFileSync(new URL('../package.json', import.meta.url), 'utf8')) as {
  main: string
  module: string
  files: string[]
  exports: Record<string, string | Record<string, string>>
}

beforeEach(() => {
  vi.resetModules()
  core.registerConsoleElement.mockClear()
  core.installScalarPluginHook.mockClear()
})

describe('the package root', () => {
  // The first import transforms every component, @scalar/components' modal and Headless UI included: about 3 s on
  // its own, longer while the rest of the suite runs in parallel.
  it('exports the documented API and registers the document element', { timeout: 30_000 }, async () => {
    // Act
    const root = await import('../src/index')

    // Assert
    expect(typeof root.createArazzoPlugin).toBe('function')
    expect(root.arazzoDocumentType.type).toBe('arazzo')
    expect(typeof root.isArazzoDocument).toBe('function')
    expect(typeof root.traverseArazzoDocument).toBe('function')
    expect(typeof root.resolveStepTarget).toBe('function')
    expect(core.registerConsoleElement).toHaveBeenCalledTimes(1)
    expect(core.registerConsoleElement.mock.calls[0]![0]).toMatchObject({ elementTag: 'bielu-arazzo-document' })
  })

  it('does not hook window.Scalar, since an importer adds the plugin itself', async () => {
    // Act
    await import('../src/index')

    // Assert
    expect(core.installScalarPluginHook).not.toHaveBeenCalled()
  })
})

describe('the <script>-tag entry', () => {
  it('re-exports the package root and hooks every Scalar API Reference to add the plugin', async () => {
    // Act
    const script = await import('../src/script')

    // Assert
    expect(typeof script.createArazzoPlugin).toBe('function')
    expect(core.registerConsoleElement).toHaveBeenCalledTimes(1)
    expect(core.installScalarPluginHook).toHaveBeenCalledTimes(1)
    const [spec, createPlugin] = core.installScalarPluginHook.mock.calls[0]!
    expect(spec).toMatchObject({ pluginName: 'bielu-arazzo' })
    expect((createPlugin as () => () => { name: string })()().name).toBe('bielu-arazzo')
  })
})

describe('package.json', () => {
  it('maps the root to the ES module build, and keeps the script-tag and pluginUrls bundles', () => {
    // Assert
    expect(packageJson.exports['.']).toMatchObject({ types: './dist/types/index.d.ts', import: './dist/index.mjs', default: './dist/index.mjs' })
    expect(packageJson.exports['./plugin.js']).toBe('./dist/plugin.js')
    expect(packageJson.exports['./scalar-plugin.mjs']).toBe('./dist/scalar-plugin.mjs')
    expect(packageJson.main).toBe('./dist/index.mjs')
    expect(packageJson.module).toBe('./dist/index.mjs')
    expect(packageJson.files).toEqual(expect.arrayContaining(['dist/index.mjs', 'dist/plugin.js', 'dist/scalar-plugin.mjs', 'dist/types']))
  })
})
